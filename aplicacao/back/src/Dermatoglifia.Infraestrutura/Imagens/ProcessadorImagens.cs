using System.Security.Cryptography;
using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Aplicacao.Excecoes;
using Dermatoglifia.Dominio.Excecoes;
using Dermatoglifia.Dominio.ObjetosDeValor;
using ImageMagick;

namespace Dermatoglifia.Infraestrutura.Imagens;

/// <summary>
/// Validação e normalização das digitais com Magick.NET (ADR-0004; doc 08, seção 6).
/// Só PNG, BMP e TIFF — formatos sem perda; JPEG é recusado para não introduzir
/// artefatos de compressão nas cristas.
/// <para>
/// Segurança: o formato é decidido pelos primeiros bytes e passado explicitamente
/// à biblioteca, que nunca "adivinha" o tipo do arquivo; as dimensões são
/// conferidas antes de decodificar os pixels.
/// </para>
/// </summary>
internal sealed class ProcessadorImagens : IProcessadorImagens
{
    public const int LadoMinimoPx = 100;
    public const int LadoMaximoPx = 4000;

    static ProcessadorImagens()
    {
        // Teto de recursos por imagem, mesmo que algo escape da verificação prévia.
        ResourceLimits.Width = LadoMaximoPx;
        ResourceLimits.Height = LadoMaximoPx;
        ResourceLimits.Area = (ulong)LadoMaximoPx * LadoMaximoPx;
    }

    public async Task<ImagemNormalizada> NormalizarAsync(Stream conteudo, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        using var memoria = new MemoryStream();
        await conteudo.CopyToAsync(memoria, cancelamento);
        var bytes = memoria.ToArray();

        // O formato é decidido pelos primeiros bytes, nunca pela extensão do arquivo.
        var (nome, formato) = FormatoPelaAssinatura(bytes)
            ?? throw new FormatoNaoSuportadoException("Formato não aceito. Envie a digital em PNG, BMP ou TIFF.");
        var leitura = new MagickReadSettings { Format = formato, FrameCount = 1 };

        MagickImageInfo informacoes;
        try
        {
            informacoes = new MagickImageInfo(bytes, leitura);
        }
        catch (MagickException)
        {
            throw new FormatoNaoSuportadoException("Arquivo de imagem corrompido ou inválido.");
        }

        // Dimensões conferidas antes de decodificar os pixels.
        Regra(informacoes.Width is >= LadoMinimoPx and <= LadoMaximoPx && informacoes.Height is >= LadoMinimoPx and <= LadoMaximoPx,
            $"A imagem deve ter entre {LadoMinimoPx} e {LadoMaximoPx} pixels de lado.");

        var resolucao = ResolucaoEmPpi(informacoes.Density);
        Regra(resolucao > 0,
            "A imagem não informa a resolução. Configure o leitor ou o scanner para gravar a resolução (ppi) no arquivo.");
        Regra(resolucao >= ArquivoDigital.ResolucaoMinimaDpi,
            $"Resolução de {resolucao} ppi; o mínimo é {ArquivoDigital.ResolucaoMinimaDpi} ppi (ADR-0004).");

        byte[] png;
        uint largura, altura;
        try
        {
            using var imagem = new MagickImage(bytes, leitura);

            // Tons de cinza em 8 bits, sem transparência.
            imagem.Alpha(AlphaAction.Remove);
            imagem.Grayscale(PixelIntensityMethod.Rec709Luminance);
            imagem.ColorSpace = ColorSpace.Gray;
            imagem.Depth = 8;

            // Remove perfis, comentários e datas. O Strip() também removeria o bloco
            // pHYs (resolução), que é mantido de propósito: o PNG final tem só
            // IHDR, pHYs, IDAT e IEND — e a mesma imagem gera sempre o mesmo hash.
            imagem.Strip();
            imagem.Density = new Density(resolucao, resolucao, DensityUnit.PixelsPerInch);
            imagem.Settings.SetDefine(MagickFormat.Png, "include-chunk", "pHYs");
            imagem.Settings.SetDefine(MagickFormat.Png, "color-type", "0");
            imagem.Settings.SetDefine(MagickFormat.Png, "bit-depth", "8");

            png = imagem.ToByteArray(MagickFormat.Png);
            (largura, altura) = (imagem.Width, imagem.Height);
        }
        catch (MagickException)
        {
            throw new FormatoNaoSuportadoException("Arquivo de imagem corrompido ou inválido.");
        }

        return new ImagemNormalizada(
            png,
            Convert.ToHexStringLower(SHA256.HashData(png)),
            nome,
            resolucao,
            (int)largura,
            (int)altura);
    }

    private static (string Nome, MagickFormat Formato)? FormatoPelaAssinatura(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => ("PNG", MagickFormat.Png),
        [0x42, 0x4D, ..] => ("BMP", MagickFormat.Bmp),
        [0x49, 0x49, 0x2A, 0x00, ..] or [0x4D, 0x4D, 0x00, 0x2A, ..] => ("TIFF", MagickFormat.Tiff),
        _ => null,
    };

    /// <summary>Menor resolução entre os dois eixos, em ppi; 0 se o arquivo não informa.</summary>
    private static int ResolucaoEmPpi(Density? densidade)
    {
        if (densidade is null)
        {
            return 0;
        }

        var fator = densidade.Units switch
        {
            DensityUnit.PixelsPerInch => 1.0,
            DensityUnit.PixelsPerCentimeter => 2.54,
            _ => 0.0, // Undefined: o arquivo não informa resolução física
        };

        return (int)Math.Round(Math.Min(densidade.X, densidade.Y) * fator);
    }

    private static void Regra(bool condicao, string mensagem)
    {
        if (!condicao)
        {
            throw new RegraDeNegocioException(mensagem);
        }
    }
}
