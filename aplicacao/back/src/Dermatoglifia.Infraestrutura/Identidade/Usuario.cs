using Dermatoglifia.Dominio.Enums;
using Microsoft.AspNetCore.Identity;

namespace Dermatoglifia.Infraestrutura.Identidade;

/// <summary>
/// Conta de acesso gerenciada pelo ASP.NET Core Identity (ADR-0006). Fica na
/// Infraestrutura para manter o Domínio sem dependências; o domínio se refere a
/// ela apenas pelo id (doc 08, seção 2).
/// </summary>
public sealed class Usuario : IdentityUser<Guid>
{
    public string Nome { get; private set; } = null!;
    public PapelUsuario Papel { get; private set; }
    public StatusCadastro Status { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? UltimoAcessoEm { get; private set; }

    /// <summary>Só contas ativas recebem token (RN01).</summary>
    public bool PodeAutenticar => Status == StatusCadastro.Ativo;

    private Usuario() { } // EF Core

    /// <summary>
    /// Cria a conta. Médicos começam PENDENTES até a aprovação (UC02/UC03);
    /// administradores são criados ativos pelo comando de inicialização.
    /// </summary>
    public static Usuario Criar(string nome, string email, PapelUsuario papel, DateTimeOffset agora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var emailNormalizado = email.Trim().ToLowerInvariant();
        return new Usuario
        {
            Id = Guid.CreateVersion7(),
            Nome = nome.Trim(),
            Email = emailNormalizado,
            UserName = emailNormalizado,
            Papel = papel,
            Status = papel == PapelUsuario.Admin ? StatusCadastro.Ativo : StatusCadastro.Pendente,
            LockoutEnabled = true,
            CriadoEm = agora,
        };
    }

    public void Ativar() => Status = StatusCadastro.Ativo;

    public void Recusar() => Status = StatusCadastro.Recusado;

    /// <summary>
    /// Inativa a conta. Quem chamar deve também atualizar o security stamp
    /// (UserManager.UpdateSecurityStampAsync) para invalidar tokens emitidos antes.
    /// </summary>
    public void Inativar() => Status = StatusCadastro.Inativo;

    public void RegistrarAcesso(DateTimeOffset agora) => UltimoAcessoEm = agora;
}
