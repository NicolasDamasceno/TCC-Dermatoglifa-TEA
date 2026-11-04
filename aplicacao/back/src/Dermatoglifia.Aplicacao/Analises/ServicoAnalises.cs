using System.Security.Cryptography;
using System.Text.Json;
using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Aplicacao.Excecoes;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.Excecoes;
using Dermatoglifia.Dominio.ObjetosDeValor;
using Dermatoglifia.Dominio.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dermatoglifia.Aplicacao.Analises;

/// <summary>
/// UC07 — Analisar digitais; UC08 — Visualizar resultado. Só o médico que conduziu
/// a sessão (o responsável pelo paciente) enxerga a análise (RN05).
/// <para>
/// O processamento usa duas gravações separadas (doc 08, seção 4): PROCESSANDO é
/// gravado antes de chamar a IA, e o resultado (CONCLUIDA ou ERRO) depois — a
/// chamada à IA nunca acontece com uma transação aberta.
/// </para>
/// </summary>
public sealed partial class ServicoAnalises(
    IContextoDados contexto,
    IClienteInferencia clienteInferencia,
    IArmazenamentoImagens armazenamento,
    TabelaConversaoAlca conversaoAlca,
    IAuditoria auditoria,
    IUsuarioAtual usuarioAtual,
    IRelogio relogio,
    ILogger<ServicoAnalises> logger)
{
    /// <summary>
    /// Processa (ou reprocessa) a análise. Falhas da IA, respostas inconsistentes e
    /// imagens ilegíveis não são erros da requisição: a análise vai para ERRO.
    /// </summary>
    public async Task<StatusAnalise> ProcessarAsync(Guid analiseId, CancellationToken cancelamento = default)
    {
        var analise = await DoMedico(rastrear: true).SingleOrDefaultAsync(a => a.Id == analiseId, cancelamento)
                      ?? throw NaoEncontrada();

        analise.IniciarProcessamento(relogio.Agora); // 422 se já concluída ou em processamento recente
        await contexto.SalvarAsync(cancelamento);

        try
        {
            var sessao = await contexto.SessoesColeta.Include(s => s.Digitais)
                .SingleAsync(s => s.Id == analise.SessaoColetaId, cancelamento);
            var sexo = await contexto.Pacientes.Where(p => p.Id == sessao.PacienteId)
                .Select(p => p.Sexo).SingleAsync(cancelamento);

            var imagens = await LerImagensAsync(sessao, cancelamento);
            var resposta = await clienteInferencia.AnalisarAsync(
                new RequisicaoInferencia(imagens, sexo, sessao.IdadeMeses), cancelamento);

            var modelo = await contexto.ModelosIA.AsNoTracking()
                             .SingleOrDefaultAsync(m => m.Versao == resposta.VersaoModelo, cancelamento)
                         ?? throw new RegraDeNegocioException(
                             $"Resposta do modelo inconsistente: a versão \"{resposta.VersaoModelo}\" não está registrada.");

            analise.Concluir(sessao, modelo, conversaoAlca, resposta.Digitais, resposta.Indicadores,
                resposta.EscoreRisco, relogio.Agora);
            contexto.ResultadosDigitais.AddRange(analise.Resultados);
            auditoria.Registrar("ANALISE_CONCLUIDA", "analise", analise.Id,
                new Dictionary<string, string> { ["versao_modelo"] = modelo.Versao });
        }
        catch (Exception e) when (e is FalhaInferenciaException or RegraDeNegocioException or ImagemIlegivelException)
        {
            analise.Falhar(e.Message);
            auditoria.Registrar("ANALISE_ERRO", "analise", analise.Id,
                new Dictionary<string, string> { ["motivo"] = CodigoMotivo(e) });
            LogAnaliseComErro(logger, analise.Id, CodigoMotivo(e));
        }

        await contexto.SalvarAsync(cancelamento);
        return analise.Status;
    }

    /// <summary>UC07, fluxo 2a: o médico pede nova tentativa após erro.</summary>
    public async Task<AnaliseDto> NovaTentativaAsync(Guid analiseId, CancellationToken cancelamento = default)
    {
        auditoria.Registrar("ANALISE_NOVA_TENTATIVA", "analise", analiseId);
        await ProcessarAsync(analiseId, cancelamento);
        return await MontarAsync(analiseId, cancelamento);
    }

    public async Task<AnaliseDto> ObterAsync(Guid analiseId, CancellationToken cancelamento = default)
    {
        var dto = await MontarAsync(analiseId, cancelamento);
        auditoria.Registrar("ANALISE_VISUALIZADA", "analise", analiseId);
        await contexto.SalvarAsync(cancelamento);
        return dto;
    }

    public async Task<AnaliseDto> RegistrarObservacaoAsync(Guid analiseId, string? observacao, CancellationToken cancelamento = default)
    {
        var analise = await DoMedico(rastrear: true).SingleOrDefaultAsync(a => a.Id == analiseId, cancelamento)
                      ?? throw NaoEncontrada();
        analise.RegistrarObservacao(observacao);
        auditoria.Registrar("ANALISE_OBSERVACAO_REGISTRADA", "analise", analiseId);
        await contexto.SalvarAsync(cancelamento);
        return await MontarAsync(analiseId, cancelamento);
    }

    /// <summary>Res. CFM 2.454/2026: o médico confirma o registro do uso de IA no prontuário.</summary>
    public async Task<AnaliseDto> ConfirmarRegistroProntuarioAsync(Guid analiseId, CancellationToken cancelamento = default)
    {
        var analise = await DoMedico(rastrear: true).SingleOrDefaultAsync(a => a.Id == analiseId, cancelamento)
                      ?? throw NaoEncontrada();
        analise.ConfirmarRegistroProntuario(relogio.Agora);
        auditoria.Registrar("ANALISE_PRONTUARIO_CONFIRMADO", "analise", analiseId);
        await contexto.SalvarAsync(cancelamento);
        return await MontarAsync(analiseId, cancelamento);
    }

    /// <summary>Ponto único de acesso: análises de sessões conduzidas pelo médico logado.</summary>
    private IQueryable<Analise> DoMedico(bool rastrear)
    {
        var medicoId = usuarioAtual.IdObrigatorio;
        var consulta = contexto.Analises.Include(a => a.Resultados)
            .Where(a => contexto.SessoesColeta.Any(s => s.Id == a.SessaoColetaId && s.MedicoId == medicoId));
        return rastrear ? consulta : consulta.AsNoTracking();
    }

    /// <summary>Decifra as 10 imagens em memória e confere o hash gravado no banco.</summary>
    private async Task<Dictionary<PosicaoDedo, byte[]>> LerImagensAsync(SessaoColeta sessao, CancellationToken cancelamento)
    {
        var imagens = new Dictionary<PosicaoDedo, byte[]>();
        foreach (var digital in sessao.Digitais.Where(d => d.Coletavel))
        {
            byte[] png;
            try
            {
                png = await armazenamento.AbrirAsync(digital.CaminhoArquivo!, cancelamento);
            }
            catch (Exception e) when (e is CryptographicException or IOException)
            {
                throw new ImagemIlegivelException(
                    $"A imagem de {digital.Posicao} não pôde ser lida (arquivo ausente, adulterado ou chave trocada).", e);
            }

            if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(png)), digital.HashSha256, StringComparison.Ordinal))
            {
                throw new ImagemIlegivelException($"A imagem de {digital.Posicao} não corresponde ao registro da coleta.");
            }

            imagens[digital.Posicao] = png;
        }

        return imagens;
    }

    private async Task<AnaliseDto> MontarAsync(Guid analiseId, CancellationToken cancelamento)
    {
        var analise = await DoMedico(rastrear: false).SingleOrDefaultAsync(a => a.Id == analiseId, cancelamento)
                      ?? throw NaoEncontrada();
        var sessao = await contexto.SessoesColeta.AsNoTracking().Include(s => s.Digitais)
            .SingleAsync(s => s.Id == analise.SessaoColetaId, cancelamento);
        var sexo = await contexto.Pacientes.Where(p => p.Id == sessao.PacienteId).Select(p => p.Sexo).SingleAsync(cancelamento);
        var modelo = analise.ModeloIAId is { } modeloId
            ? await contexto.ModelosIA.AsNoTracking().SingleAsync(m => m.Id == modeloId, cancelamento)
            : null;

        var versao = analise.VersaoAviso;
        var concluida = analise.Status == StatusAnalise.Concluida;
        var codigos = new List<string> { CodigosAlerta.SemPontoDeCorteValidado };
        if (concluida && analise.EscoreRisco is null)
        {
            codigos.Add(CodigosAlerta.EscoreNaoValidado);
        }

        if (concluida && modelo?.OrigemDados == OrigemDados.Sintetico)
        {
            codigos.Add(CodigosAlerta.ModeloSintetico);
        }

        if (sexo == SexoBiologico.Feminino)
        {
            codigos.Add(CodigosAlerta.PoucosDadosFemininos);
        }

        if (analise.Resultados.Any(r => r.AlertaQualidade))
        {
            codigos.Add(CodigosAlerta.QualidadeImagem);
        }

        var digitalPorId = sessao.Digitais.ToDictionary(d => d.Id);
        var digitais = analise.Resultados
            .Select(r => (Digital: digitalPorId[r.DigitalId], Resultado: r))
            .OrderBy(x => PosicaoDedoIndice(x.Digital.Posicao))
            .Select(x => new ResultadoDedoDto(
                x.Digital.Mao, x.Digital.Dedo, x.Resultado.PadraoPrevisto, x.Resultado.Confianca, x.Resultado.AlertaQualidade))
            .ToList();

        return new AnaliseDto(
            analise.Id,
            sessao.Id,
            sessao.PacienteId,
            analise.Status,
            new AvisoDto(versao, AvisosResultado.Aviso(versao)),
            codigos.Select(c => new AlertaDto(c, AvisosResultado.Alerta(versao, c))).ToList(),
            modelo is null
                ? null
                : new ModeloDto(modelo.Versao, modelo.OrigemDados, modelo.EscoreValidado, modelo.TreinadoEm,
                    JsonDocument.Parse(modelo.MetricasTeste).RootElement.Clone()),
            digitais,
            analise.Indicadores,
            analise.EscoreRisco,
            analise.ObservacaoMedico,
            analise.RegistradoProntuarioEm,
            analise.MensagemErro,
            analise.SolicitadaEm,
            analise.ConcluidaEm);
    }

    private static int PosicaoDedoIndice(PosicaoDedo posicao)
    {
        for (var i = 0; i < PosicaoDedo.OrdemColeta.Count; i++)
        {
            if (PosicaoDedo.OrdemColeta[i] == posicao)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static string CodigoMotivo(Exception e) => e switch
    {
        FalhaInferenciaException => "IA_INDISPONIVEL_OU_INVALIDA",
        ImagemIlegivelException => "IMAGEM_ILEGIVEL",
        _ => "RESPOSTA_INCONSISTENTE",
    };

    private static NaoEncontradoException NaoEncontrada() => new("Análise não encontrada.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Análise {AnaliseId} terminou com erro ({Motivo}).")]
    private static partial void LogAnaliseComErro(ILogger logger, Guid analiseId, string motivo);

    private sealed class ImagemIlegivelException(string mensagem, Exception? causa = null) : Exception(mensagem, causa);
}

public sealed record AnaliseDto(
    Guid Id,
    Guid SessaoId,
    Guid PacienteId,
    StatusAnalise Status,
    AvisoDto Aviso,
    IReadOnlyList<AlertaDto> Alertas,
    ModeloDto? Modelo,
    IReadOnlyList<ResultadoDedoDto> Digitais,
    IndicadoresDermatoglificos? Indicadores,
    decimal? EscoreRisco,
    string? ObservacaoMedico,
    DateTimeOffset? RegistradoProntuarioEm,
    string? MensagemErro,
    DateTimeOffset SolicitadaEm,
    DateTimeOffset? ConcluidaEm);

public sealed record AvisoDto(string Versao, string Texto);

public sealed record AlertaDto(string Codigo, string Texto);

public sealed record ModeloDto(string Versao, OrigemDados OrigemDados, bool EscoreValidado, DateOnly TreinadoEm, JsonElement Metricas);

public sealed record ResultadoDedoDto(Mao Mao, Dedo Dedo, PadraoDigital Padrao, decimal Confianca, bool AlertaQualidade);
