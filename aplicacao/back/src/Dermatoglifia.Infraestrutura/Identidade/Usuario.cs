using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.Excecoes;
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
    public static Usuario Criar(string nome, string email, PapelUsuario papel, DateTimeOffset agora, Guid? id = null)
    {
        var nomeLimpo = nome?.Trim();
        if (string.IsNullOrEmpty(nomeLimpo) || nomeLimpo.Length > 150)
        {
            throw new RegraDeNegocioException("Nome é obrigatório e deve ter no máximo 150 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new RegraDeNegocioException("E-mail é obrigatório.");
        }

        var emailNormalizado = email.Trim().ToLowerInvariant();
        return new Usuario
        {
            Id = id ?? Guid.CreateVersion7(),
            Nome = nomeLimpo,
            Email = emailNormalizado,
            UserName = emailNormalizado,
            Papel = papel,
            Status = papel == PapelUsuario.Admin ? StatusCadastro.Ativo : StatusCadastro.Pendente,
            LockoutEnabled = true,
            CriadoEm = agora,
        };
    }

    /// <summary>
    /// Muda o status. Qualquer status diferente de ATIVO troca o security stamp,
    /// o que invalida na hora os tokens emitidos antes (ADR-0006).
    /// </summary>
    public void AlterarStatus(StatusCadastro status)
    {
        Status = status;
        if (status != StatusCadastro.Ativo)
        {
            SecurityStamp = Guid.NewGuid().ToString("N").ToUpperInvariant();
        }
    }

    public void RegistrarAcesso(DateTimeOffset agora) => UltimoAcessoEm = agora;
}
