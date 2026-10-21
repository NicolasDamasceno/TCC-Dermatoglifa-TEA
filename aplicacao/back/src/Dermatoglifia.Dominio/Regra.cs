using Dermatoglifia.Dominio.Excecoes;

namespace Dermatoglifia.Dominio;

/// <summary>Verificações curtas usadas pelas entidades para proteger suas regras.</summary>
internal static class Regra
{
    public static void Exigir(bool condicao, string mensagem)
    {
        if (!condicao)
        {
            throw new RegraDeNegocioException(mensagem);
        }
    }

    public static string TextoObrigatorio(string? valor, string campo, int tamanhoMaximo)
    {
        var texto = valor?.Trim();
        Exigir(!string.IsNullOrEmpty(texto), $"{campo} é obrigatório.");
        Exigir(texto!.Length <= tamanhoMaximo, $"{campo} deve ter no máximo {tamanhoMaximo} caracteres.");
        return texto;
    }

    public static string? TextoOpcional(string? valor, string campo, int tamanhoMaximo)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            return null;
        }

        Exigir(texto.Length <= tamanhoMaximo, $"{campo} deve ter no máximo {tamanhoMaximo} caracteres.");
        return texto;
    }
}
