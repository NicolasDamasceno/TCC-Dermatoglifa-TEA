using System.Net;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dermatoglifia.Aplicacao.Abstracoes;

/// <summary>Hora atual em UTC. Abstraída para os testes controlarem o tempo.</summary>
public interface IRelogio
{
    DateTimeOffset Agora { get; }
}

/// <summary>Quem está fazendo a requisição (preenchido pela API a partir do token).</summary>
public interface IUsuarioAtual
{
    Guid? Id { get; }
    PapelUsuario? Papel { get; }
    IPAddress? Ip { get; }

    Guid IdObrigatorio => Id ?? throw new InvalidOperationException("Operação exige usuário autenticado.");
}

/// <summary>
/// Acesso às entidades do domínio. A Aplicação usa as abstrações de consulta do
/// EF Core (<see cref="DbSet{TEntity}"/>), mas não conhece o provedor do banco.
/// </summary>
public interface IContextoDados
{
    DbSet<Medico> Medicos { get; }
    DbSet<Paciente> Pacientes { get; }
    DbSet<IdentificacaoPaciente> Identificacoes { get; }
    DbSet<Consentimento> Consentimentos { get; }
    DbSet<SessaoColeta> SessoesColeta { get; }
    DbSet<Digital> Digitais { get; }
    DbSet<ModeloIA> ModelosIA { get; }
    DbSet<Analise> Analises { get; }
    DbSet<ResultadoDigital> ResultadosDigitais { get; }

    Task<int> SalvarAsync(CancellationToken cancelamento = default);

    Task<IDbContextTransaction> IniciarTransacaoAsync(CancellationToken cancelamento = default);
}

/// <summary>
/// Trilha de auditoria (RNF07). O registro é adicionado à unidade de trabalho e
/// gravado no próximo <see cref="IContextoDados.SalvarAsync"/>, na mesma transação
/// da operação. Só chaves permitidas de <paramref name="detalhes"/> são gravadas.
/// </summary>
public interface IAuditoria
{
    void Registrar(
        string acao,
        string entidade,
        Guid? entidadeId,
        IReadOnlyDictionary<string, string>? detalhes = null,
        Guid? usuarioId = null);
}

/// <summary>Contas de acesso (ASP.NET Core Identity, na Infraestrutura — ADR-0006).</summary>
public interface IContas
{
    /// <summary>
    /// Cria a conta de um médico com status PENDENTE e grava imediatamente.
    /// Senha fora da política → <see cref="Dominio.Excecoes.RegraDeNegocioException"/>;
    /// e-mail já usado → <see cref="Excecoes.ConflitoException"/>.
    /// </summary>
    Task CriarMedicoPendenteAsync(Guid id, string nome, string email, string senha, CancellationToken cancelamento = default);

    Task<ResultadoCredenciais> VerificarCredenciaisAsync(string email, string senha, CancellationToken cancelamento = default);

    /// <summary>Muda o status da conta (não grava; a gravação é da unidade de trabalho).</summary>
    Task AlterarStatusAsync(Guid id, StatusCadastro status, CancellationToken cancelamento = default);

    Task<IReadOnlyDictionary<Guid, ResumoConta>> ObterResumosAsync(IEnumerable<Guid> ids, CancellationToken cancelamento = default);

    /// <summary>
    /// Confere, a cada requisição, se o token ainda vale: conta ativa e o resumo do
    /// security stamp igual ao do token (<see cref="Autenticacao.CarimboSeguranca"/>).
    /// </summary>
    Task<bool> SessaoValidaAsync(Guid id, string resumoCarimbo, CancellationToken cancelamento = default);
}

public sealed record ResumoConta(Guid Id, string Nome, string Email, PapelUsuario Papel, StatusCadastro Status, DateTimeOffset CriadoEm);

public sealed record ContaAutenticada(Guid Id, string Nome, string Email, PapelUsuario Papel, string CarimboSeguranca);

public enum SituacaoCredenciais { Validas, Invalidas, Bloqueada, ContaNaoAtiva }

public sealed record ResultadoCredenciais(SituacaoCredenciais Situacao, Guid? UsuarioId, ContaAutenticada? Conta, StatusCadastro? Status)
{
    public static ResultadoCredenciais Invalidas(Guid? usuarioId) => new(SituacaoCredenciais.Invalidas, usuarioId, null, null);
    public static ResultadoCredenciais Bloqueada(Guid usuarioId) => new(SituacaoCredenciais.Bloqueada, usuarioId, null, null);
    public static ResultadoCredenciais NaoAtiva(Guid usuarioId, StatusCadastro status) => new(SituacaoCredenciais.ContaNaoAtiva, usuarioId, null, status);
    public static ResultadoCredenciais Validas(ContaAutenticada conta) => new(SituacaoCredenciais.Validas, conta.Id, conta, null);
}

/// <summary>Emite o token de acesso (JWT, ADR-0006).</summary>
public interface IEmissorToken
{
    TokenEmitido Emitir(ContaAutenticada conta);
}

public sealed record TokenEmitido(string Token, DateTimeOffset ExpiraEm);
