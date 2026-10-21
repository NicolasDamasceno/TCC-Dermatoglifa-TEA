using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dermatoglifia.Aplicacao.Abstracoes;
using Microsoft.Extensions.Configuration;

namespace Dermatoglifia.Infraestrutura.Imagens;

/// <summary>Configuração <c>Armazenamento:*</c>. Chaves nunca ficam no Git.</summary>
public sealed class OpcoesArmazenamento
{
    public const int TamanhoChaveBytes = 32; // AES-256

    public required string Diretorio { get; init; }
    public required IReadOnlyDictionary<byte, byte[]> Chaves { get; init; }
    public required byte VersaoAtual { get; init; }

    /// <summary>Lê e valida na inicialização: sem diretório ou chave válida, a API não sobe.</summary>
    public static OpcoesArmazenamento Carregar(IConfiguration configuracao)
    {
        var secao = configuracao.GetSection("Armazenamento");
        var diretorio = secao["Diretorio"];
        if (string.IsNullOrWhiteSpace(diretorio))
        {
            throw new InvalidOperationException(
                "Armazenamento:Diretorio não configurado: informe uma pasta fora do repositório e da pasta pública.");
        }

        var chaves = new Dictionary<byte, byte[]>();
        foreach (var item in secao.GetSection("Chaves").GetChildren())
        {
            if (!byte.TryParse(item.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var versao) || versao == 0)
            {
                throw new InvalidOperationException($"Armazenamento:Chaves:{item.Key}: a versão da chave deve ser um número de 1 a 255.");
            }

            byte[] chave;
            try
            {
                chave = Convert.FromBase64String(item.Value ?? string.Empty);
            }
            catch (FormatException)
            {
                chave = [];
            }

            if (chave.Length != TamanhoChaveBytes)
            {
                throw new InvalidOperationException(
                    $"Armazenamento:Chaves:{item.Key} deve ter exatamente {TamanhoChaveBytes} bytes em base64 (openssl rand -base64 32).");
            }

            chaves[versao] = chave;
        }

        if (chaves.Count == 0)
        {
            throw new InvalidOperationException(
                "Nenhuma chave em Armazenamento:Chaves. Defina Armazenamento:Chaves:1 com 32 bytes aleatórios em base64.");
        }

        var atual = secao["ChaveAtual"] is { } texto
            ? byte.Parse(texto, CultureInfo.InvariantCulture)
            : chaves.Keys.Max();
        if (!chaves.ContainsKey(atual))
        {
            throw new InvalidOperationException($"Armazenamento:ChaveAtual = {atual}, mas não há chave com essa versão.");
        }

        return new OpcoesArmazenamento { Diretorio = Path.GetFullPath(diretorio), Chaves = chaves, VersaoAtual = atual };
    }
}

/// <summary>
/// Arquivos cifrados com AES-256-GCM (RNF02; doc 08, seção 6). Formato:
/// <c>[versão da chave: 1][nonce: 12][tag: 16][dados cifrados]</c>. O caminho
/// relativo entra como dado associado: um arquivo copiado para o lugar de outro
/// (por exemplo, a digital de um dedo no lugar de outro) não decifra.
/// </summary>
internal sealed class ArmazenamentoLocalCifrado : IArmazenamentoImagens
{
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;
    private const int TamanhoCabecalho = 1 + TamanhoNonce + TamanhoTag;

    private readonly OpcoesArmazenamento _opcoes;
    private readonly string _raiz;

    public ArmazenamentoLocalCifrado(OpcoesArmazenamento opcoes)
    {
        _opcoes = opcoes;
        _raiz = Path.TrimEndingDirectorySeparator(opcoes.Diretorio) + Path.DirectorySeparatorChar;
        CriarDiretorioRestrito(opcoes.Diretorio);
    }

    public async Task SalvarAsync(string caminhoRelativo, byte[] conteudo, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        var (completo, associado) = Resolver(caminhoRelativo);

        var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce);
        var tag = new byte[TamanhoTag];
        var cifrado = new byte[conteudo.Length];
        using (var aes = new AesGcm(_opcoes.Chaves[_opcoes.VersaoAtual], TamanhoTag))
        {
            aes.Encrypt(nonce, conteudo, cifrado, tag, associado);
        }

        var arquivo = new byte[TamanhoCabecalho + cifrado.Length];
        arquivo[0] = _opcoes.VersaoAtual;
        nonce.CopyTo(arquivo, 1);
        tag.CopyTo(arquivo, 1 + TamanhoNonce);
        cifrado.CopyTo(arquivo, TamanhoCabecalho);

        CriarDiretorioRestrito(Path.GetDirectoryName(completo)!);

        // Grava num temporário e move: nunca fica um arquivo pela metade no lugar final.
        var temporario = $"{completo}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllBytesAsync(temporario, arquivo, cancelamento);
        RestringirArquivo(temporario);
        File.Move(temporario, completo, overwrite: false);
    }

    public async Task<byte[]> AbrirAsync(string caminhoRelativo, CancellationToken cancelamento = default)
    {
        var (completo, associado) = Resolver(caminhoRelativo);
        var arquivo = await File.ReadAllBytesAsync(completo, cancelamento);
        if (arquivo.Length < TamanhoCabecalho)
        {
            throw new CryptographicException("Arquivo cifrado incompleto.");
        }

        if (!_opcoes.Chaves.TryGetValue(arquivo[0], out var chave))
        {
            throw new CryptographicException($"Arquivo cifrado com a chave versão {arquivo[0]}, que não está configurada.");
        }

        var nonce = arquivo.AsSpan(1, TamanhoNonce);
        var tag = arquivo.AsSpan(1 + TamanhoNonce, TamanhoTag);
        var cifrado = arquivo.AsSpan(TamanhoCabecalho);
        var claro = new byte[cifrado.Length];
        using (var aes = new AesGcm(chave, TamanhoTag))
        {
            aes.Decrypt(nonce, cifrado, tag, claro, associado); // adulteração → AuthenticationTagMismatchException
        }

        return claro;
    }

    public Task ApagarAsync(string caminhoRelativo, CancellationToken cancelamento = default)
    {
        var (completo, _) = Resolver(caminhoRelativo);
        File.Delete(completo); // não falha se o arquivo já não existir
        return Task.CompletedTask;
    }

    /// <summary>Caminho absoluto dentro da raiz e o dado associado da cifragem.</summary>
    private (string Completo, byte[] Associado) Resolver(string caminhoRelativo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoRelativo);
        var normalizado = caminhoRelativo.Replace('\\', '/');
        if (Path.IsPathRooted(normalizado) || normalizado.Split('/').Any(parte => parte is ".." or "." or ""))
        {
            throw new ArgumentException("Caminho de armazenamento inválido.", nameof(caminhoRelativo));
        }

        var completo = Path.GetFullPath(Path.Combine(_raiz, normalizado));
        if (!completo.StartsWith(_raiz, StringComparison.Ordinal))
        {
            throw new ArgumentException("Caminho fora do diretório de armazenamento.", nameof(caminhoRelativo));
        }

        return (completo, Encoding.UTF8.GetBytes(normalizado));
    }

    private static void CriarDiretorioRestrito(string diretorio)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(diretorio);
        }
        else
        {
            Directory.CreateDirectory(diretorio, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void RestringirArquivo(string arquivo)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(arquivo, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
