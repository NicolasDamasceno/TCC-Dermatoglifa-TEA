using System.Security.Cryptography;
using System.Text;

namespace Dermatoglifia.Aplicacao.Autenticacao;

/// <summary>
/// O token leva apenas um resumo (SHA-256) do security stamp da conta, nunca o
/// valor em si. A cada requisição o resumo é comparado com o da conta: se o stamp
/// mudou (conta inativada, recusada, senha trocada), o token deixa de valer.
/// </summary>
public static class CarimboSeguranca
{
    public static string Resumo(string carimbo)
    {
        ArgumentNullException.ThrowIfNull(carimbo);
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(carimbo)));
    }

    public static bool Confere(string carimboAtual, string resumoDoToken)
    {
        var esperado = Encoding.UTF8.GetBytes(Resumo(carimboAtual));
        var recebido = Encoding.UTF8.GetBytes(resumoDoToken);
        return CryptographicOperations.FixedTimeEquals(esperado, recebido);
    }
}
