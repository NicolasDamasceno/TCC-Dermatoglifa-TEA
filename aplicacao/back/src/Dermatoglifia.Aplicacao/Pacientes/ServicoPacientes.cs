using System.Globalization;
using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Aplicacao.Excecoes;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dermatoglifia.Aplicacao.Pacientes;

/// <summary>
/// UC04 — Cadastrar paciente; UC05 — Registrar consentimento; UC13 — Atualizar
/// situação diagnóstica. Leituras partem de <see cref="Acessiveis"/>: o médico só
/// enxerga os próprios pacientes (o de outro médico é tratado como inexistente) e o
/// administrador enxerga todos (RN05; ADR-0010). Escritas clínicas partem de
/// <see cref="DoMedico"/>, só do médico responsável.
/// </summary>
public sealed class ServicoPacientes(
    IContextoDados contexto,
    IAuditoria auditoria,
    IUsuarioAtual usuarioAtual,
    IRelogio relogio)
{
    public async Task<Guid> CadastrarAsync(NovoPaciente dados, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(dados);

        var responsavel = dados.Responsavel is null
            ? null
            : new DadosResponsavel(dados.Responsavel.Nome, dados.Responsavel.Parentesco, dados.Responsavel.Contato);
        var paciente = Paciente.Cadastrar(
            usuarioAtual.IdObrigatorio, dados.Sexo, dados.Nome, dados.DataNascimento, responsavel, relogio.Agora);

        contexto.Pacientes.Add(paciente);
        auditoria.Registrar("PACIENTE_CRIADO", "paciente", paciente.Id);
        await contexto.SalvarAsync(cancelamento);
        return paciente.Id;
    }

    public async Task<IReadOnlyList<PacienteResumo>> ListarAsync(string? busca, CancellationToken cancelamento = default)
    {
        var consulta = Acessiveis().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLower(CultureInfo.InvariantCulture);
            // Traduzido para SQL pelo EF Core (lower(nome) LIKE '%termo%'): os
            // avisos de cultura/comparação valem para código em memória, não aqui.
#pragma warning disable CA1304, CA1311, CA1862
            consulta = consulta.Where(p => p.Identificacao != null && p.Identificacao.Nome.ToLower().Contains(termo));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var pacientes = await consulta.OrderByDescending(p => p.CriadoEm).ToListAsync(cancelamento);

        // Listar mostra nomes: é leitura de dado pessoal e fica registrada (RNF07).
        auditoria.Registrar("PACIENTES_LISTADOS", "paciente", null,
            new Dictionary<string, string> { ["quantidade"] = pacientes.Count.ToString(CultureInfo.InvariantCulture) });
        await contexto.SalvarAsync(cancelamento);

        var hoje = Hoje();
        return pacientes.Select(p => Resumir(p, hoje)).ToList();
    }

    public async Task<PacienteDetalhe> ObterAsync(Guid pacienteId, CancellationToken cancelamento = default)
    {
        var paciente = await Acessiveis().AsNoTracking().SingleOrDefaultAsync(p => p.Id == pacienteId, cancelamento)
                       ?? throw NaoEncontrado();

        auditoria.Registrar("PACIENTE_VISUALIZADO", "paciente", paciente.Id);
        await contexto.SalvarAsync(cancelamento);

        // Sessões do paciente (mais recente primeiro) com a análise de cada uma, para
        // retomar uma coleta em andamento ou abrir um resultado a partir do paciente.
        var sessoes = await contexto.SessoesColeta.AsNoTracking()
            .Where(s => s.PacienteId == paciente.Id)
            .OrderByDescending(s => s.IniciadaEm)
            .Select(s => new
            {
                s.Id,
                s.Status,
                s.IniciadaEm,
                s.ConcluidaEm,
                Analise = contexto.Analises
                    .Where(a => a.SessaoColetaId == s.Id)
                    .Select(a => new
                    {
                        a.Id,
                        a.Status,
                        a.ConcluidaEm,
                        // Versão do modelo que produziu cada análise (UC09; RN06).
                        Modelo = contexto.ModelosIA
                            .Where(m => m.Id == a.ModeloIAId)
                            .Select(m => new { m.Versao, m.OrigemDados })
                            .FirstOrDefault(),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancelamento);

        var identificacao = paciente.Identificacao;
        var responsavel = identificacao?.ResponsavelNome is null
            ? null
            : new ResponsavelDto(identificacao.ResponsavelNome, identificacao.ResponsavelParentesco, identificacao.ResponsavelContato);

        return new PacienteDetalhe(
            Resumir(paciente, Hoje()),
            responsavel,
            paciente.Situacao == SituacaoDiagnostica.Suspeita
                ? null
                : new DiagnosticoDto(paciente.FonteDiagnostico!, paciente.DiagnosticoAvaliadoEm, paciente.SituacaoAtualizadaEm),
            paciente.Consentimentos
                .OrderByDescending(c => c.AssinadoEm)
                .Select(c => new ConsentimentoDto(
                    c.Id, c.Tipo, c.VersaoTermo, c.AssinadoEm, c.AutorizaAnalise, c.CienteUsoIA,
                    c.AutorizaUsoTreino, c.EstaVigente, c.RevogadoEm))
                .ToList(),
            sessoes
                .Select(s => new SessaoDoPaciente(
                    s.Id, s.Status, s.IniciadaEm, s.ConcluidaEm, s.Analise?.Id, s.Analise?.Status,
                    s.Analise?.ConcluidaEm, s.Analise?.Modelo?.Versao, s.Analise?.Modelo?.OrigemDados))
                .ToList());
    }

    public async Task<ResultadoConsentimento> RegistrarConsentimentoAsync(
        Guid pacienteId, NovoConsentimento dados, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var paciente = await DoMedico().SingleOrDefaultAsync(p => p.Id == pacienteId, cancelamento) ?? throw NaoEncontrado();

        var consentimento = paciente.RegistrarConsentimento(
            dados.Tipo, dados.VersaoTermo, dados.AssinadoEm, dados.AutorizaAnalise, dados.CienteUsoIA,
            dados.AutorizaUsoTreino, usuarioAtual.IdObrigatorio, relogio.Agora);

        // A chave é gerada no domínio; marcar como novo explicitamente.
        contexto.Consentimentos.Add(consentimento);
        auditoria.Registrar("CONSENTIMENTO_REGISTRADO", "paciente", paciente.Id,
            new Dictionary<string, string> { ["tipo"] = dados.Tipo.ToString().ToUpperInvariant() });
        await contexto.SalvarAsync(cancelamento);

        return new ResultadoConsentimento(consentimento.Id, paciente.PodeColetar);
    }

    /// <summary>UC05 (3b) e UC12: o médico responsável ou o administrador, a pedido do titular.</summary>
    public async Task<ResultadoConsentimento> RevogarConsentimentoAsync(
        Guid pacienteId, Guid consentimentoId, CancellationToken cancelamento = default)
    {
        var paciente = await Acessiveis().SingleOrDefaultAsync(p => p.Id == pacienteId, cancelamento) ?? throw NaoEncontrado();

        paciente.RevogarConsentimento(consentimentoId, relogio.Agora);
        auditoria.Registrar("CONSENTIMENTO_REVOGADO", "paciente", paciente.Id);
        await contexto.SalvarAsync(cancelamento);

        return new ResultadoConsentimento(consentimentoId, paciente.PodeColetar);
    }

    /// <summary>
    /// UC13 — registra a situação definida pelo diagnóstico clínico, feito fora do
    /// sistema (ADR-0009). A auditoria guarda a transição, não a fonte (texto livre).
    /// </summary>
    public async Task<SituacaoAtualizada> AtualizarSituacaoAsync(
        Guid pacienteId, NovaSituacao dados, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var paciente = await DoMedico().SingleOrDefaultAsync(p => p.Id == pacienteId, cancelamento) ?? throw NaoEncontrado();

        var anterior = paciente.Situacao;
        paciente.AtualizarSituacao(dados.Situacao, dados.Fonte, dados.AvaliadoEm, dados.IndependenteDoSistema, relogio.Agora);

        auditoria.Registrar("SITUACAO_DIAGNOSTICA_ATUALIZADA", "paciente", paciente.Id,
            new Dictionary<string, string>
            {
                ["situacao_anterior"] = TextoEnum(anterior),
                ["situacao_nova"] = TextoEnum(paciente.Situacao),
            });
        await contexto.SalvarAsync(cancelamento);

        return new SituacaoAtualizada(
            paciente.Situacao, paciente.FonteDiagnostico, paciente.DiagnosticoAvaliadoEm, paciente.SituacaoAtualizadaEm!.Value);
    }

    /// <summary>Pacientes que o usuário pode ver: todos para o administrador, os próprios para o médico.</summary>
    private IQueryable<Paciente> Acessiveis()
        => usuarioAtual.Papel == PapelUsuario.Admin
            ? contexto.Pacientes.Include(p => p.Identificacao).Include(p => p.Consentimentos)
            : DoMedico();

    /// <summary>Só os pacientes do médico logado (escritas clínicas).</summary>
    private IQueryable<Paciente> DoMedico()
    {
        var medicoId = usuarioAtual.IdObrigatorio;
        return contexto.Pacientes
            .Include(p => p.Identificacao)
            .Include(p => p.Consentimentos)
            .Where(p => p.MedicoResponsavelId == medicoId);
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(relogio.Agora.UtcDateTime);

    /// <summary>Mesmo texto que a API usa para o enum (TEA_CONFIRMADO).</summary>
    private static string TextoEnum(SituacaoDiagnostica situacao) => situacao switch
    {
        SituacaoDiagnostica.Suspeita => "SUSPEITA",
        SituacaoDiagnostica.TeaConfirmado => "TEA_CONFIRMADO",
        SituacaoDiagnostica.TeaDescartado => "TEA_DESCARTADO",
        SituacaoDiagnostica.Controle => "CONTROLE",
        _ => throw new ArgumentOutOfRangeException(nameof(situacao)),
    };

    private static NaoEncontradoException NaoEncontrado() => new("Paciente não encontrado.");

    private static PacienteResumo Resumir(Paciente paciente, DateOnly hoje)
    {
        var identificacao = paciente.Identificacao;
        return new PacienteResumo(
            paciente.Id,
            identificacao?.Nome,
            identificacao?.DataNascimento,
            identificacao is null ? null : identificacao.IdadeEmMeses(hoje) / 12,
            paciente.Sexo,
            paciente.Situacao,
            paciente.PodeColetar,
            paciente.Anonimizado,
            paciente.CriadoEm);
    }
}

public sealed record NovoPaciente(string Nome, DateOnly DataNascimento, SexoBiologico Sexo, NovoResponsavel? Responsavel);

public sealed record NovoResponsavel(string? Nome, string? Parentesco, string? Contato);

public sealed record NovoConsentimento(
    TipoTermo Tipo, string VersaoTermo, DateOnly AssinadoEm, bool AutorizaAnalise, bool CienteUsoIA, bool AutorizaUsoTreino);

public sealed record ResultadoConsentimento(Guid ConsentimentoId, bool PodeColetar);

public sealed record NovaSituacao(SituacaoDiagnostica Situacao, string? Fonte, DateOnly? AvaliadoEm, bool IndependenteDoSistema);

public sealed record SituacaoAtualizada(
    SituacaoDiagnostica Situacao, string? FonteDiagnostico, DateOnly? AvaliadoEm, DateTimeOffset AtualizadaEm);

/// <summary>Como a situação foi definida; nulo enquanto o paciente é suspeita.</summary>
public sealed record DiagnosticoDto(string Fonte, DateOnly? AvaliadoEm, DateTimeOffset? AtualizadoEm);

/// <summary>
/// <see cref="Nome"/> e <see cref="DataNascimento"/> são nulos quando o titular pediu
/// a eliminação dos dados (UC12).
/// </summary>
public sealed record PacienteResumo(
    Guid Id,
    string? Nome,
    DateOnly? DataNascimento,
    int? IdadeAnos,
    SexoBiologico Sexo,
    SituacaoDiagnostica Situacao,
    bool PodeColetar,
    bool Anonimizado,
    DateTimeOffset CriadoEm);

public sealed record ResponsavelDto(string Nome, string? Parentesco, string? Contato);

public sealed record ConsentimentoDto(
    Guid Id,
    TipoTermo Tipo,
    string VersaoTermo,
    DateOnly AssinadoEm,
    bool AutorizaAnalise,
    bool CienteUsoIA,
    bool AutorizaUsoTreino,
    bool Vigente,
    DateTimeOffset? RevogadoEm);

public sealed record PacienteDetalhe(
    PacienteResumo Paciente,
    ResponsavelDto? Responsavel,
    DiagnosticoDto? Diagnostico,
    IReadOnlyList<ConsentimentoDto> Consentimentos,
    IReadOnlyList<SessaoDoPaciente> Sessoes);

/// <summary>
/// Resumo de uma sessão de coleta no detalhe do paciente (UC09): a análise, quando
/// concluída, traz a versão e a origem dos dados do modelo que a produziu.
/// </summary>
public sealed record SessaoDoPaciente(
    Guid Id,
    StatusSessao Status,
    DateTimeOffset IniciadaEm,
    DateTimeOffset? ConcluidaEm,
    Guid? AnaliseId,
    StatusAnalise? AnaliseStatus,
    DateTimeOffset? AnaliseConcluidaEm,
    string? VersaoModelo,
    OrigemDados? OrigemDadosModelo);
