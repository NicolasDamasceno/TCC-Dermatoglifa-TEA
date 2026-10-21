using System.Net.Http.Headers;
using ImageMagick;

namespace Dermatoglifia.Api.Testes;

/// <summary>
/// Imagens sintéticas (ruído) no lugar de digitais: nenhuma digital real entra nos
/// testes (RNF08). Cada chamada gera um conteúdo diferente.
/// </summary>
public static class ImagensSinteticas
{
    public static byte[] Gerar(
        MagickFormat formato = MagickFormat.Png,
        int ppi = 500,
        uint lado = 300,
        bool comResolucao = true,
        bool colorida = false)
    {
        using var imagem = new MagickImage(colorida ? MagickColors.Orange : MagickColors.Gray, lado, lado);
        imagem.AddNoise(NoiseType.Gaussian, (double)Random.Shared.Next(1, 5));

        if (comResolucao)
        {
            imagem.Density = new Density(ppi, ppi, DensityUnit.PixelsPerInch);
        }
        else
        {
            imagem.Density = new Density(0, 0, DensityUnit.Undefined);
            imagem.Settings.SetDefine(MagickFormat.Png, "exclude-chunk", "pHYs");
        }

        return imagem.ToByteArray(formato);
    }

    public static MultipartFormDataContent Formulario(byte[] conteudo, string nomeArquivo = "dedo.png")
    {
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { arquivo, "imagem", nomeArquivo } };
    }
}
