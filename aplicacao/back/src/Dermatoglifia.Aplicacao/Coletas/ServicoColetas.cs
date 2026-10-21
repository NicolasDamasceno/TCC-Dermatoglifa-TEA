using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Aplicacao.Excecoes;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.Excecoes;
using Dermatoglifia.Dominio.ObjetosDeValor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dermatoglifia.Aplicacao.Coletas;

/// <summary>
/// UC06 — Coletar as 10 digitais. Só o médico responsável pelo paciente conduz e
/// enxerga a sessão (RN05). Arquivos de imagem são gravados antes do banco e
/// apagados se a gravação no banco falhar; o arquivo substituído só é apagado
/// depois que o banco confirmou a troca.
/// </summary>
public sealed partial class ServicoColetas(
    IContextoDados contexto,
    IProcessadorImagens processador,
    IArmazenamentoImagens armazenamento,
    IAuditoria auditoria,
    IUsuarioAtual usuarioAtual,
    IRelogio relogio,
    ILogger<ServicoColetas> logger)
{
    /// <summary>Tamanho máximo de uma imagem enviada (ADR-0004).</summary>
    public const long TamanhoMaximoBytes = 5 * 1024 * 1024;

    public async Task<SessaoDto> IniciarAsync(Guid pacienteId, NovaSessao dados, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var medicoId = usuarioAtual.IdObrigatorio;

        var paciente = await contexto.Pacientes
            .Include(p => p.Identificacao)
            .Include(p => p.Consentimentos)
            .SingleOrDefaultAsync(p => p.Id == pacienteId && p.MedicoResponsavelId == medicoId, cancelamento)
            ?? throw new NaoEncontradoException("Paciente não encontrado.");

        if (await contexto.SessoesColeta.AnyAsync(
                s => s.PacienteId == pacienteId && s.Status == StatusSessao.EmAndamento, cancelamento))
        {
            throw new ConflitoException("Já existe uma sessão de coleta em andamento para este paciente.");
        }

        var sessao = SessaoColeta.Iniciar(paciente, medicoId, dados.Metodo, dados.TipoImpressao, dados.Dispositivo, relogio.Agora);
        contexto.SessoesColeta.Add(sessao);
        auditoria.Registrar("SESSAO_INICIADA", "sessao_coleta", sessao.Id);
        await contexto.SalvarAsync(cancelamento);

        return Mapear(sessao);
    }

    public async Task<SessaoDto> ObterAsync(Guid sessaoId, CancellationToken cancelamento = default)
        => Mapear(await CarregarAsync(sessaoId, rastrear: false, cancelamento));

    /// <summary>Envia (ou refaz) a imagem de um dedo.</summary>
    public async Task<SessaoDto> EnviarDigitalAsync(
        Guid sessaoId, Mao mao, Dedo dedo, Stream conteudo, long tamanhoBytes, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        if (tamanhoBytes <= 0)
        {
            throw new RegraDeNegocioException("Arquivo de imagem vazio.");
        }

        if (tamanhoBytes > TamanhoMaximoBytes)
        {
            throw new ConteudoMuitoGrandeException($"A imagem passa de {TamanhoMaximoBytes / (1024 * 1024)} MB.");
        }

        var posicao = new PosicaoDedo(mao, dedo);
        var sessao = await CarregarAsync(sessaoId, rastrear: true, cancelamento);
        GarantirEmAndamento(sessao);
        await GarantirConsentimentoVigenteAsync(sessao.PacienteId, cancelamento);

        var imagem = await processador.NormalizarAsync(conteudo, cancelamento);

        // Nome único por envio: a gravação no banco decide qual arquivo vale.
        var caminho = $"{sessao.Id:N}/{posicao.NomeArquivo}_{Guid.NewGuid():N}.png.enc";
        await armazenamento.SalvarAsync(caminho, imagem.Png, cancelamento);

        string? caminhoAnterior;
        try
        {
            var eraNovo = !sessao.Digitais.Any(d => d.Mao == mao && d.Dedo == dedo);
            caminhoAnterior = sessao.RegistrarDigital(
                posicao,
                new ArquivoDigital(caminho, imagem.HashSha256, "PNG", imagem.ResolucaoDpi, imagem.LarguraPx, imagem.AlturaPx),
                relogio.Agora);
            if (eraNovo)
            {
                contexto.Digitais.Add(sessao.Digitais.Single(d => d.Mao == mao && d.Dedo == dedo));
            }

            auditoria.Registrar("DIGITAL_ENVIADA", "sessao_coleta", sessao.Id, Detalhes(posicao));
            await contexto.SalvarAsync(cancelamento);
        }
        catch
        {
            await ApagarSemFalharAsync(caminho);
            throw;
        }

        await ApagarSemFalharAsync(caminhoAnterior);
        return Mapear(sessao);
    }

    /// <summary>Dedo impossível de coletar (amputação, lesão, malformação) — UC06, 4b.</summary>
    public async Task<SessaoDto> MarcarNaoColetavelAsync(
        Guid sessaoId, Mao mao, Dedo dedo, string motivo, CancellationToken cancelamento = default)
    {
        var posicao = new PosicaoDedo(mao, dedo);
        var sessao = await CarregarAsync(sessaoId, rastrear: true, cancelamento);

        var eraNovo = !sessao.Digitais.Any(d => d.Mao == mao && d.Dedo == dedo);
        var caminhoAnterior = sessao.MarcarNaoColetavel(posicao, motivo, relogio.Agora);
        if (eraNovo)
        {
            contexto.Digitais.Add(sessao.Digitais.Single(d => d.Mao == mao && d.Dedo == dedo));
        }

        auditoria.Registrar("DEDO_NAO_COLETAVEL", "sessao_coleta", sessao.Id, Detalhes(posicao));
        await contexto.SalvarAsync(cancelamento);

        await ApagarSemFalharAsync(caminhoAnterior);
        return Mapear(sessao);
    }

    /// <summary>
    /// Conclui a sessão: COMPLETA com os 10 dedos, INCOMPLETA se algum foi
    /// marcado como não coletável (RN09). A análise (UC07) entra no marco M5.
    /// </summary>
    public async Task<SessaoDto> ConcluirAsync(Guid sessaoId, CancellationToken cancelamento = default)
    {
        var sessao = await CarregarAsync(sessaoId, rastrear: true, cancelamento);
        var status = sessao.Concluir(relogio.Agora);

        auditoria.Registrar("SESSAO_CONCLUIDA", "sessao_coleta", sessao.Id,
            new Dictionary<string, string> { ["status"] = status.ToString().ToUpperInvariant() });
        await contexto.SalvarAsync(cancelamento);
        return Mapear(sessao);
    }

    public async Task<SessaoDto> CancelarAsync(Guid sessaoId, CancellationToken cancelamento = default)
    {
        var sessao = await CarregarAsync(sessaoId, rastrear: true, cancelamento);
        sessao.Cancelar(relogio.Agora);

        auditoria.Registrar("SESSAO_CANCELADA", "sessao_coleta", sessao.Id);
        await contexto.SalvarAsync(cancelamento);
        return Mapear(sessao);
    }

    /// <summary>Ponto único de acesso às sessões: só as conduzidas pelo médico logado.</summary>
    private async Task<SessaoColeta> CarregarAsync(Guid sessaoId, bool rastrear, CancellationToken cancelamento)
    {
        var medicoId = usuarioAtual.IdObrigatorio;
        var consulta = contexto.SessoesColeta.Include(s => s.Digitais).Where(s => s.Id == sessaoId && s.MedicoId == medicoId);
        if (!rastrear)
        {
            consulta = consulta.AsNoTracking();
        }

        return await consulta.SingleOrDefaultAsync(cancelamento)
               ?? throw new NaoEncontradoException("Sessão de coleta não encontrada.");
    }

    private static void GarantirEmAndamento(SessaoColeta sessao)
    {
        if (sessao.Status != StatusSessao.EmAndamento)
        {
            throw new RegraDeNegocioException("A sessão de coleta não está em andamento.");
        }
    }

    /// <summary>Se o consentimento foi revogado no meio da sessão, a coleta para (RN03).</summary>
    private async Task GarantirConsentimentoVigenteAsync(Guid pacienteId, CancellationToken cancelamento)
    {
        var paciente = await contexto.Pacientes.AsNoTracking()
            .Include(p => p.Consentimentos)
            .SingleAsync(p => p.Id == pacienteId, cancelamento);

        if (!paciente.PodeColetar)
        {
            throw new RegraDeNegocioException(
                "O consentimento para a análise não está mais vigente; a coleta não pode continuar.");
        }
    }

    private async Task ApagarSemFalharAsync(string? caminho)
    {
        if (caminho is null)
        {
            return;
        }

        try
        {
            await armazenamento.ApagarAsync(caminho, CancellationToken.None);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Não derruba a requisição: o banco já está correto; o arquivo órfão fica no log.
            LogFalhaAoApagar(logger, caminho, e);
        }
    }

    private static Dictionary<string, string> Detalhes(PosicaoDedo posicao) => new()
    {
        ["mao"] = posicao.Mao.ToString().ToUpperInvariant(),
        ["dedo"] = posicao.Dedo.ToString().ToUpperInvariant(),
    };

    private static SessaoDto Mapear(SessaoColeta sessao)
    {
        var dedos = PosicaoDedo.OrdemColeta.Select(posicao =>
        {
            var digital = sessao.Digitais.SingleOrDefault(d => d.Mao == posicao.Mao && d.Dedo == posicao.Dedo);
            var situacao = digital switch
            {
                null => SituacaoDedo.Pendente,
                { Coletavel: true } => SituacaoDedo.Coletado,
                _ => SituacaoDedo.NaoColetavel,
            };
            return new DedoDto(posicao.Mao, posicao.Dedo, situacao, digital?.ResolucaoDpi, digital?.MotivoNaoColetavel, digital?.CapturadaEm);
        }).ToList();

        return new SessaoDto(
            sessao.Id, sessao.PacienteId, sessao.Status, sessao.Metodo, sessao.TipoImpressao, sessao.Dispositivo,
            sessao.IdadeMeses, sessao.IniciadaEm, sessao.ConcluidaEm, dedos);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível apagar o arquivo substituído {Caminho}.")]
    private static partial void LogFalhaAoApagar(ILogger logger, string caminho, Exception excecao);
}

public sealed record NovaSessao(MetodoCaptura Metodo, TipoImpressao TipoImpressao, string? Dispositivo);

public enum SituacaoDedo { Pendente, Coletado, NaoColetavel }

/// <summary>Estado de um dedo. Imagens nunca são devolvidas pela API (doc 06).</summary>
public sealed record DedoDto(
    Mao Mao, Dedo Dedo, SituacaoDedo Situacao, int? ResolucaoDpi, string? MotivoNaoColetavel, DateTimeOffset? CapturadaEm);

public sealed record SessaoDto(
    Guid Id,
    Guid PacienteId,
    StatusSessao Status,
    MetodoCaptura Metodo,
    TipoImpressao TipoImpressao,
    string? Dispositivo,
    int IdadeMeses,
    DateTimeOffset IniciadaEm,
    DateTimeOffset? ConcluidaEm,
    IReadOnlyList<DedoDto> Dedos);
