using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Aplicacao.Excecoes;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Excecoes;
using Microsoft.EntityFrameworkCore;

namespace Dermatoglifia.Aplicacao.Pacientes;

/// <summary>
/// UC12 — Atender pedido do titular (LGPD art. 18). Só o administrador (ADR-0010):
/// exporta os dados do paciente (acesso e portabilidade), corrige a identificação e
/// elimina identificação, imagens e textos livres, mantendo só dados pseudônimos (RN08).
/// A revogação de consentimento usa a rota do UC05, liberada também ao administrador.
/// </summary>
public sealed class ServicoTitular(
    IContextoDados contexto,
    IArmazenamentoImagens armazenamento,
    IAuditoria auditoria,
    IRelogio relogio)
{
    /// <summary>Texto que o administrador digita para confirmar a eliminação.</summary>
    public const string ConfirmacaoEliminacao = "ELIMINAR";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    /// <summary>
    /// Pacote .zip com <c>dados.json</c> (todos os dados do paciente no sistema) e as
    /// imagens das digitais, decifradas, em <c>digitais/</c>.
    /// </summary>
    public async Task<ArquivoExportado> ExportarAsync(Guid pacienteId, CancellationToken cancelamento = default)
    {
        var paciente = await Paciente(pacienteId, rastrear: false, cancelamento);
        var sessoes = await contexto.SessoesColeta.AsNoTracking()
            .Include(s => s.Digitais)
            .Where(s => s.PacienteId == pacienteId)
            .OrderBy(s => s.IniciadaEm)
            .ToListAsync(cancelamento);
        var idsSessoes = sessoes.Select(s => s.Id).ToList();
        var analises = await contexto.Analises.AsNoTracking()
            .Include(a => a.Resultados)
            .Where(a => idsSessoes.Contains(a.SessaoColetaId))
            .ToListAsync(cancelamento);
        var idsModelos = analises.Where(a => a.ModeloIAId is not null).Select(a => a.ModeloIAId!.Value).ToList();
        var modelos = await contexto.ModelosIA.AsNoTracking()
            .Where(m => idsModelos.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Versao, cancelamento);

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            var imagens = new List<string>();
            foreach (var sessao in sessoes)
            {
                foreach (var digital in sessao.Digitais.Where(d => d.CaminhoArquivo is not null))
                {
                    var nome = $"digitais/{sessao.IniciadaEm:yyyy-MM-dd_HHmm}_{Texto(digital.Mao)}_{Texto(digital.Dedo)}.png";
                    var entrada = zip.CreateEntry(nome, CompressionLevel.Optimal);
                    await using var destino = await entrada.OpenAsync(cancelamento);
                    await destino.WriteAsync(await armazenamento.AbrirAsync(digital.CaminhoArquivo!, cancelamento), cancelamento);
                    imagens.Add(nome);
                }
            }

            var dados = MontarDados(paciente, sessoes, analises, modelos, imagens);
            var json = zip.CreateEntry("dados.json", CompressionLevel.Optimal);
            await using (var destino = await json.OpenAsync(cancelamento))
            {
                await JsonSerializer.SerializeAsync(destino, dados, Json, cancelamento);
            }
        }

        auditoria.Registrar("PACIENTE_EXPORTADO", "paciente", pacienteId);
        await contexto.SalvarAsync(cancelamento);

        var nomeArquivo = $"paciente_{pacienteId:N}_{relogio.Agora:yyyyMMdd}.zip";
        return new ArquivoExportado(nomeArquivo, memoria.ToArray());
    }

    public async Task CorrigirIdentificacaoAsync(Guid pacienteId, NovaIdentificacao dados, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var paciente = await Paciente(pacienteId, rastrear: true, cancelamento);
        var responsavel = dados.Responsavel is null
            ? null
            : new DadosResponsavel(dados.Responsavel.Nome, dados.Responsavel.Parentesco, dados.Responsavel.Contato);

        paciente.CorrigirIdentificacao(dados.Nome, dados.DataNascimento, responsavel, relogio.Agora);
        // Sem os valores: o log não guarda dados pessoais (RNF07).
        auditoria.Registrar("IDENTIFICACAO_CORRIGIDA", "paciente", pacienteId);
        await contexto.SalvarAsync(cancelamento);
    }

    /// <summary>
    /// Elimina identificação, imagens e textos livres. Ficam o paciente pseudônimo,
    /// os consentimentos (prova do que foi autorizado), a situação diagnóstica e os
    /// resultados da análise (RN08). Irreversível.
    /// </summary>
    public async Task<int> EliminarAsync(Guid pacienteId, string? confirmacao, CancellationToken cancelamento = default)
    {
        if (confirmacao?.Trim() != ConfirmacaoEliminacao)
        {
            throw new RegraDeNegocioException($"Para confirmar a eliminação, digite {ConfirmacaoEliminacao}.");
        }

        var paciente = await Paciente(pacienteId, rastrear: true, cancelamento);
        var sessoes = await contexto.SessoesColeta.Include(s => s.Digitais)
            .Where(s => s.PacienteId == pacienteId)
            .ToListAsync(cancelamento);
        var idsSessoes = sessoes.Select(s => s.Id).ToList();
        var analises = await contexto.Analises.Where(a => idsSessoes.Contains(a.SessaoColetaId)).ToListAsync(cancelamento);

        var agora = relogio.Agora;
        paciente.Anonimizar(agora);
        var caminhos = sessoes.SelectMany(s => s.EliminarDadosDoTitular(agora)).ToList();
        foreach (var analise in analises)
        {
            analise.EliminarTextoLivre();
        }

        auditoria.Registrar("PACIENTE_ELIMINADO", "paciente", pacienteId,
            new Dictionary<string, string> { ["quantidade"] = caminhos.Count.ToString(CultureInfo.InvariantCulture) });

        // Grava, apaga os arquivos e só então confirma: se apagar falhar, nada é
        // confirmado e o pedido pode ser repetido (apagar de novo não falha).
        await using var transacao = await contexto.IniciarTransacaoAsync(cancelamento);
        await contexto.SalvarAsync(cancelamento);
        foreach (var caminho in caminhos)
        {
            await armazenamento.ApagarAsync(caminho, cancelamento);
        }

        await transacao.CommitAsync(cancelamento);
        return caminhos.Count;
    }

    private async Task<Paciente> Paciente(Guid pacienteId, bool rastrear, CancellationToken cancelamento)
    {
        var consulta = contexto.Pacientes.Include(p => p.Identificacao).Include(p => p.Consentimentos);
        var paciente = await (rastrear ? consulta : consulta.AsNoTracking())
                           .SingleOrDefaultAsync(p => p.Id == pacienteId, cancelamento)
                       ?? throw new NaoEncontradoException("Paciente não encontrado.");
        if (paciente.Anonimizado)
        {
            throw new RegraDeNegocioException("Os dados deste paciente já foram eliminados a pedido do titular.");
        }

        return paciente;
    }

    private static object MontarDados(
        Paciente p,
        List<SessaoColeta> sessoes,
        List<Analise> analises,
        Dictionary<Guid, string> modelos,
        List<string> imagens)
    {
        var id = p.Identificacao!;
        return new
        {
            Aviso = "Dados do participante no protótipo de pesquisa Dermatoglifia × TEA. O resultado da análise não é diagnóstico.",
            Paciente = new
            {
                p.Id,
                id.Nome,
                id.DataNascimento,
                p.Sexo,
                SituacaoDiagnostica = p.Situacao,
                p.FonteDiagnostico,
                p.DiagnosticoAvaliadoEm,
                p.CriadoEm,
            },
            Responsavel = id.ResponsavelNome is null
                ? null
                : new { Nome = id.ResponsavelNome, Parentesco = id.ResponsavelParentesco, Contato = id.ResponsavelContato },
            Consentimentos = p.Consentimentos.OrderBy(c => c.AssinadoEm).Select(c => new
            {
                c.Tipo, c.VersaoTermo, c.AssinadoEm, c.AutorizaAnalise, c.CienteUsoIA, c.AutorizaUsoTreino, c.RevogadoEm,
            }),
            Coletas = sessoes.Select(s =>
            {
                var analise = analises.SingleOrDefault(a => a.SessaoColetaId == s.Id);
                return new
                {
                    s.Status, s.Metodo, s.TipoImpressao, s.Dispositivo, s.IdadeMeses, s.IniciadaEm, s.ConcluidaEm, s.Observacoes,
                    Digitais = s.Digitais.Select(d => new
                    {
                        d.Mao, d.Dedo, d.Coletavel, d.MotivoNaoColetavel, d.ResolucaoDpi, d.CapturadaEm,
                    }),
                    Analise = analise is null
                        ? null
                        : new
                        {
                            analise.Status,
                            VersaoModelo = analise.ModeloIAId is { } m && modelos.TryGetValue(m, out var v) ? v : null,
                            analise.Indicadores,
                            analise.EscoreRisco,
                            analise.ObservacaoMedico,
                            analise.RegistradoProntuarioEm,
                            analise.ConcluidaEm,
                            Resultados = analise.Resultados.Select(r => new
                            {
                                Digital = Posicao(s.Digitais.Single(d => d.Id == r.DigitalId)),
                                Padrao = r.PadraoPrevisto,
                                r.Confianca,
                                r.AlertaQualidade,
                            }),
                        },
                };
            }),
            Imagens = imagens,
        };
    }

    private static string Posicao(Digital d) => $"{Texto(d.Mao)}_{Texto(d.Dedo)}";

    private static string Texto<T>(T valor) where T : struct, Enum => JsonNamingPolicy.SnakeCaseUpper.ConvertName(valor.ToString());
}

public sealed record ArquivoExportado(string Nome, byte[] Conteudo);

/// <summary>Dados corrigíveis da identificação; o sexo biológico fica no paciente pseudônimo.</summary>
public sealed record NovaIdentificacao(string Nome, DateOnly DataNascimento, NovoResponsavel? Responsavel);
