namespace Dermatoglifia.Aplicacao.Abstracoes;

/// <summary>
/// Valida e normaliza a imagem de uma digital (ADR-0004; doc 08, seção 6):
/// formato decidido pelos bytes, resolução mínima, conversão para tons de cinza
/// de 8 bits e PNG sem metadados.
/// </summary>
public interface IProcessadorImagens
{
    /// <exception cref="Excecoes.FormatoNaoSuportadoException">Não é PNG, BMP ou TIFF válido.</exception>
    /// <exception cref="Dominio.Excecoes.RegraDeNegocioException">Resolução ausente/baixa ou dimensões fora do limite.</exception>
    Task<ImagemNormalizada> NormalizarAsync(Stream conteudo, CancellationToken cancelamento = default);
}

/// <param name="Png">PNG em tons de cinza (8 bits), sem metadados além da resolução.</param>
/// <param name="HashSha256">SHA-256 do <paramref name="Png"/>, em hexadecimal minúsculo.</param>
public sealed record ImagemNormalizada(
    byte[] Png, string HashSha256, string FormatoOriginal, int ResolucaoDpi, int LarguraPx, int AlturaPx);

/// <summary>
/// Guarda as imagens cifradas fora da pasta pública (RNF02). Os caminhos são
/// relativos ao diretório configurado; o conteúdo nunca é gravado em claro.
/// </summary>
public interface IArmazenamentoImagens
{
    Task SalvarAsync(string caminhoRelativo, byte[] conteudo, CancellationToken cancelamento = default);

    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Arquivo adulterado, trocado de lugar ou cifrado com chave desconhecida.
    /// </exception>
    Task<byte[]> AbrirAsync(string caminhoRelativo, CancellationToken cancelamento = default);

    Task ApagarAsync(string caminhoRelativo, CancellationToken cancelamento = default);
}
