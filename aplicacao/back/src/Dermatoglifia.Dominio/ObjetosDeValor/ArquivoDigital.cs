namespace Dermatoglifia.Dominio.ObjetosDeValor;

/// <summary>
/// Metadados de uma imagem de digital já validada, normalizada e cifrada.
/// O conteúdo nunca passa pelo domínio — só a referência e o hash.
/// </summary>
public sealed record ArquivoDigital
{
    /// <summary>Resolução mínima aceita (ANSI/NIST-ITL 1-2000, seção 5.1; ADR-0004).</summary>
    public const int ResolucaoMinimaDpi = 500;

    public string CaminhoArquivo { get; }
    public string HashSha256 { get; }
    public string Formato { get; }
    public int ResolucaoDpi { get; }
    public int LarguraPx { get; }
    public int AlturaPx { get; }

    public ArquivoDigital(string caminhoArquivo, string hashSha256, string formato, int resolucaoDpi, int larguraPx, int alturaPx)
    {
        CaminhoArquivo = Regra.TextoObrigatorio(caminhoArquivo, "Caminho do arquivo", 300);
        Regra.Exigir(
            !Path.IsPathRooted(CaminhoArquivo) && !CaminhoArquivo.Contains("..", StringComparison.Ordinal),
            "O caminho do arquivo deve ser relativo ao diretório de armazenamento.");

        HashSha256 = Regra.TextoObrigatorio(hashSha256, "Hash", 64).ToLowerInvariant();
        Regra.Exigir(HashSha256.Length == 64 && HashSha256.All(Uri.IsHexDigit), "Hash SHA-256 inválido.");

        Formato = Regra.TextoObrigatorio(formato, "Formato", 10).ToUpperInvariant();
        Regra.Exigir(
            resolucaoDpi >= ResolucaoMinimaDpi,
            $"A imagem precisa ter pelo menos {ResolucaoMinimaDpi} ppi.");
        Regra.Exigir(larguraPx > 0 && alturaPx > 0, "Dimensões da imagem inválidas.");

        ResolucaoDpi = resolucaoDpi;
        LarguraPx = larguraPx;
        AlturaPx = alturaPx;
    }
}
