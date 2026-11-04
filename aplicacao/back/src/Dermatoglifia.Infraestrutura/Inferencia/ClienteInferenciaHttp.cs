using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Aplicacao.Excecoes;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.ObjetosDeValor;
using Dermatoglifia.Dominio.Servicos;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dermatoglifia.Infraestrutura.Inferencia;

/// <summary>Configuração <c>Inferencia:*</c>, validada na inicialização.</summary>
public sealed class OpcoesInferencia
{
    public required Uri UrlBase { get; init; }
    public required TimeSpan Timeout { get; init; }
    public required TabelaConversaoAlca ConversaoAlca { get; init; }

    public static OpcoesInferencia Carregar(IConfiguration configuracao)
    {
        var secao = configuracao.GetSection("Inferencia");
        var url = secao["UrlBase"];
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "Inferencia:UrlBase não configurada ou inválida: informe o endereço do Serviço de IA (ex.: http://localhost:8000).");
        }

        var segundos = secao.GetValue("TimeoutSegundos", 30);
        if (segundos is < 1 or > 300)
        {
            throw new InvalidOperationException("Inferencia:TimeoutSegundos deve estar entre 1 e 300.");
        }

        var conversao = (secao["ConvencaoAlca"] ?? "IMPRESSAO").ToUpperInvariant() switch
        {
            "IMPRESSAO" => TabelaConversaoAlca.Impressao,
            "ESPELHADA" => TabelaConversaoAlca.Espelhada,
            var outro => throw new InvalidOperationException(
                $"Inferencia:ConvencaoAlca = \"{outro}\"; use IMPRESSAO ou ESPELHADA (ADR-0002)."),
        };

        // Barra final: "inferencia" é resolvido relativo à base, inclusive com caminho.
        var baseComBarra = new Uri(uri.AbsoluteUri.EndsWith('/') ? uri.AbsoluteUri : uri.AbsoluteUri + "/");
        return new OpcoesInferencia { UrlBase = baseComBarra, Timeout = TimeSpan.FromSeconds(segundos), ConversaoAlca = conversao };
    }
}

/// <summary>
/// Cliente do Serviço de IA (ADR-0001; doc 08, seção 7). Uma nova tentativa só em
/// erro de rede; resposta de erro, tempo esgotado ou JSON inválido viram
/// <see cref="FalhaInferenciaException"/>. Nunca registra o conteúdo das imagens.
/// </summary>
internal sealed partial class ClienteInferenciaHttp(HttpClient http, ILogger<ClienteInferenciaHttp> logger) : IClienteInferencia
{
    public const string Rota = "inferencia";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<RespostaInferencia> AnalisarAsync(RequisicaoInferencia requisicao, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        for (var tentativa = 1; ; tentativa++)
        {
            // O conteúdo é montado a cada tentativa: um multipart já enviado não pode ser reenviado.
            using var conteudo = MontarConteudo(requisicao);
            try
            {
                using var resposta = await http.PostAsync(Rota, conteudo, cancelamento);
                if (!resposta.IsSuccessStatusCode)
                {
                    throw new FalhaInferenciaException(
                        $"O Serviço de IA respondeu com erro (HTTP {(int)resposta.StatusCode}).");
                }

                var corpo = await resposta.Content.ReadFromJsonAsync<RespostaJson>(Json, cancelamento)
                            ?? throw new FalhaInferenciaException("O Serviço de IA devolveu uma resposta vazia.");
                return Converter(corpo);
            }
            catch (HttpRequestException e) when (tentativa == 1)
            {
                LogNovaTentativa(logger, e.Message);
            }
            catch (HttpRequestException e)
            {
                throw new FalhaInferenciaException("Serviço de IA indisponível. Tente novamente em instantes.", e);
            }
            catch (TaskCanceledException e) when (!cancelamento.IsCancellationRequested)
            {
                throw new FalhaInferenciaException("O Serviço de IA não respondeu dentro do tempo limite.", e);
            }
            catch (JsonException e)
            {
                throw new FalhaInferenciaException("Resposta do Serviço de IA em formato inválido.", e);
            }
        }
    }

    /// <summary>10 arquivos <c>MAO_DEDO</c> em PNG + <c>sexo</c> + <c>idade_meses</c>, nada mais.</summary>
    private static MultipartFormDataContent MontarConteudo(RequisicaoInferencia requisicao)
    {
        var conteudo = new MultipartFormDataContent();
        foreach (var posicao in PosicaoDedo.OrdemColeta)
        {
            if (!requisicao.Imagens.TryGetValue(posicao, out var png))
            {
                conteudo.Dispose();
                throw new ArgumentException($"Falta a imagem de {posicao}.", nameof(requisicao));
            }

            var arquivo = new ByteArrayContent(png);
            arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            conteudo.Add(arquivo, posicao.NomeArquivo, $"{posicao.NomeArquivo}.png");
        }

        conteudo.Add(new StringContent(EnumComoTexto.ParaTexto(requisicao.Sexo)), "sexo");
        conteudo.Add(new StringContent(requisicao.IdadeMeses.ToString(CultureInfo.InvariantCulture)), "idade_meses");
        return conteudo;
    }

    private static RespostaInferencia Converter(RespostaJson corpo)
    {
        try
        {
            var digitais = corpo.Digitais.Select(d => new ResultadoDedoRecebido(
                    new PosicaoDedo(EnumComoTexto.DeTexto<Mao>(d.Mao), EnumComoTexto.DeTexto<Dedo>(d.Dedo)),
                    EnumComoTexto.DeTexto<ClasseImagem>(d.ClasseImagem),
                    EnumComoTexto.DeTexto<PadraoDigital>(d.Padrao),
                    d.Confianca,
                    d.Probabilidades.ToDictionary(p => EnumComoTexto.DeTexto<ClasseImagem>(p.Key), p => p.Value),
                    d.AlertaQualidade))
                .ToList();

            var i = corpo.Indicadores;
            var indicadores = new IndicadoresDermatoglificos(
                i.QtdArcos, i.QtdArcosTendidos, i.QtdAlcasUlnares, i.QtdAlcasRadiais, i.QtdVerticilos,
                i.DiscordanciaBilateral, i.DiscordanciaBilateralComPolegar);

            return new RespostaInferencia(corpo.VersaoModelo, digitais, indicadores, corpo.EscoreRisco);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            // Valor de enum desconhecido, chave repetida etc.
            throw new FalhaInferenciaException("Resposta do Serviço de IA em formato inválido.", e);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha de rede ao chamar o Serviço de IA ({Motivo}); nova tentativa.")]
    private static partial void LogNovaTentativa(ILogger logger, string motivo);

    // Formato JSON do ADR-0001 (snake_case). "required": campo ausente → JsonException.
    private sealed record RespostaJson
    {
        public required string VersaoModelo { get; init; }
        public required List<DigitalJson> Digitais { get; init; }
        public required IndicadoresJson Indicadores { get; init; }
        public decimal? EscoreRisco { get; init; }
    }

    private sealed record DigitalJson
    {
        public required string Mao { get; init; }
        public required string Dedo { get; init; }
        public required string ClasseImagem { get; init; }
        public required string Padrao { get; init; }
        public required decimal Confianca { get; init; }
        public required Dictionary<string, decimal> Probabilidades { get; init; }
        public bool AlertaQualidade { get; init; }
    }

    private sealed record IndicadoresJson
    {
        public required int QtdArcos { get; init; }
        public required int QtdArcosTendidos { get; init; }
        public required int QtdAlcasUlnares { get; init; }
        public required int QtdAlcasRadiais { get; init; }
        public required int QtdVerticilos { get; init; }
        public required int DiscordanciaBilateral { get; init; }
        public required int DiscordanciaBilateralComPolegar { get; init; }
    }
}
