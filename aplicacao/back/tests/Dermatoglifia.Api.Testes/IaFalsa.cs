using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.ObjetosDeValor;
using Dermatoglifia.Dominio.Servicos;

namespace Dermatoglifia.Api.Testes;

/// <summary>O que a API enviou ao Serviço de IA numa chamada.</summary>
public sealed record ChamadaIa(
    string Metodo,
    string Caminho,
    IReadOnlyDictionary<string, ArquivoEnviado> Arquivos,
    IReadOnlyDictionary<string, string> Campos);

public sealed record ArquivoEnviado(string? NomeArquivo, string? TipoConteudo, byte[] Conteudo);

/// <summary>
/// Serviço de IA falso para os testes de integração. Por padrão responde de forma
/// consistente (todas as digitais com <see cref="ClassePadrao"/>, salvo as de
/// <see cref="ClassePorDedo"/>); cada teste pode trocar <see cref="Responder"/>.
/// Implementa o contrato do ADR-0001 do lado do serviço.
/// </summary>
public sealed class IaFalsa
{
    private readonly ConcurrentQueue<ChamadaIa> _chamadas = new();

    public Func<ChamadaIa, CancellationToken, Task<HttpResponseMessage>>? Responder { get; set; }
    public ClasseImagem ClassePadrao { get; set; } = ClasseImagem.Verticilo;
    public Dictionary<PosicaoDedo, ClasseImagem> ClassePorDedo { get; } = [];
    public HashSet<PosicaoDedo> AlertaQualidadeEm { get; } = [];
    public string Versao { get; set; } = AmbienteApi.VersaoModeloTeste;
    public decimal? Escore { get; set; }

    public IReadOnlyList<ChamadaIa> Chamadas => [.. _chamadas];

    public void Reiniciar()
    {
        Responder = null;
        ClassePadrao = ClasseImagem.Verticilo;
        ClassePorDedo.Clear();
        AlertaQualidadeEm.Clear();
        Versao = AmbienteApi.VersaoModeloTeste;
        Escore = null;
        _chamadas.Clear();
    }

    internal async Task<HttpResponseMessage> AtenderAsync(ChamadaIa chamada, CancellationToken cancelamento)
    {
        _chamadas.Enqueue(chamada);
        return Responder is null
            ? Json(RespostaConsistente(chamada))
            : await Responder(chamada, cancelamento);
    }

    /// <summary>Resposta coerente com as regras do ADR-0002 para as imagens recebidas.</summary>
    public Dictionary<string, object?> RespostaConsistente(ChamadaIa chamada)
    {
        var conversao = TabelaConversaoAlca.Impressao;
        var padroes = new Dictionary<PosicaoDedo, PadraoDigital>();
        var digitais = new List<Dictionary<string, object?>>();

        foreach (var nome in chamada.Arquivos.Keys)
        {
            var partes = nome.Split('_');
            var posicao = new PosicaoDedo(Enum.Parse<Mao>(partes[0], ignoreCase: true), Enum.Parse<Dedo>(partes[1], ignoreCase: true));
            var classe = ClassePorDedo.TryGetValue(posicao, out var c) ? c : ClassePadrao;
            var padrao = conversao.Converter(classe, posicao.Mao);
            padroes[posicao] = padrao;

            digitais.Add(new Dictionary<string, object?>
            {
                ["mao"] = Texto(posicao.Mao),
                ["dedo"] = Texto(posicao.Dedo),
                ["classe_imagem"] = Texto(classe),
                ["padrao"] = Texto(padrao),
                ["confianca"] = 0.8m,
                ["probabilidades"] = Enum.GetValues<ClasseImagem>().ToDictionary(Texto, k => k == classe ? 0.8m : 0.05m),
                ["alerta_qualidade"] = AlertaQualidadeEm.Contains(posicao),
            });
        }

        var i = IndicadoresDermatoglificos.Calcular(padroes);
        return new Dictionary<string, object?>
        {
            ["versao_modelo"] = Versao,
            ["digitais"] = digitais,
            ["indicadores"] = new Dictionary<string, int>
            {
                ["qtd_arcos"] = i.QtdArcos,
                ["qtd_arcos_tendidos"] = i.QtdArcosTendidos,
                ["qtd_alcas_ulnares"] = i.QtdAlcasUlnares,
                ["qtd_alcas_radiais"] = i.QtdAlcasRadiais,
                ["qtd_verticilos"] = i.QtdVerticilos,
                ["discordancia_bilateral"] = i.DiscordanciaBilateral,
                ["discordancia_bilateral_com_polegar"] = i.DiscordanciaBilateralComPolegar,
            },
            ["escore_risco"] = Escore,
        };
    }

    public static HttpResponseMessage Json(object corpo, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json") };

    /// <summary>ARCO_TENDIDO, ALCA_DIREITA etc.</summary>
    public static string Texto<TEnum>(TEnum valor) where TEnum : struct, Enum
        => JsonNamingPolicy.SnakeCaseUpper.ConvertName(valor.ToString());
}

/// <summary>Recebe as requisições do HttpClient da API e entrega à <see cref="IaFalsa"/>.</summary>
public sealed class HandlerIaFalsa(IaFalsa ia) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var arquivos = new Dictionary<string, ArquivoEnviado>();
        var campos = new Dictionary<string, string>();
        if (request.Content is MultipartFormDataContent multipart)
        {
            foreach (var parte in multipart)
            {
                var disposicao = parte.Headers.ContentDisposition!;
                var nome = disposicao.Name!.Trim('"');
                if (disposicao.FileName is { } arquivo)
                {
                    arquivos[nome] = new ArquivoEnviado(
                        arquivo.Trim('"'), parte.Headers.ContentType?.MediaType, await parte.ReadAsByteArrayAsync(cancellationToken));
                }
                else
                {
                    campos[nome] = await parte.ReadAsStringAsync(cancellationToken);
                }
            }
        }

        return await ia.AtenderAsync(
            new ChamadaIa(request.Method.Method, request.RequestUri!.AbsolutePath, arquivos, campos), cancellationToken);
    }
}
