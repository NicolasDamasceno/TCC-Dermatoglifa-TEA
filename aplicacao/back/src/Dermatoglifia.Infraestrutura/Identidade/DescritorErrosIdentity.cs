using Microsoft.AspNetCore.Identity;

namespace Dermatoglifia.Infraestrutura.Identidade;

/// <summary>Mensagens do Identity em português, exibidas ao médico no cadastro.</summary>
internal sealed class DescritorErrosIdentity : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email)
        => Erro(nameof(DuplicateEmail), "Já existe cadastro com este e-mail.");

    public override IdentityError DuplicateUserName(string userName)
        => Erro(nameof(DuplicateUserName), "Já existe cadastro com este e-mail.");

    public override IdentityError InvalidEmail(string? email)
        => Erro(nameof(InvalidEmail), "E-mail inválido.");

    public override IdentityError InvalidUserName(string? userName)
        => Erro(nameof(InvalidUserName), "E-mail inválido.");

    public override IdentityError PasswordTooShort(int length)
        => Erro(nameof(PasswordTooShort), $"A senha deve ter pelo menos {length} caracteres.");

    public override IdentityError PasswordRequiresDigit()
        => Erro(nameof(PasswordRequiresDigit), "A senha deve conter pelo menos um número.");

    public override IdentityError PasswordRequiresLower()
        => Erro(nameof(PasswordRequiresLower), "A senha deve conter pelo menos uma letra minúscula.");

    public override IdentityError PasswordRequiresUpper()
        => Erro(nameof(PasswordRequiresUpper), "A senha deve conter pelo menos uma letra maiúscula.");

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => Erro(nameof(PasswordRequiresNonAlphanumeric), "A senha deve conter pelo menos um símbolo.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => Erro(nameof(PasswordRequiresUniqueChars), $"A senha deve ter pelo menos {uniqueChars} caracteres diferentes.");

    private static IdentityError Erro(string codigo, string descricao) => new() { Code = codigo, Description = descricao };
}
