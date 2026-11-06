using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dermatoglifia.Api.Testes;

/// <summary>
/// Regras que valem para a API inteira: autenticação em todas as rotas, formato
/// dos erros, CORS, cabeçalhos de segurança e limite de requisições.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class ApiTransversalTestes(AmbienteApi ambiente)
{
    /// <summary>As únicas rotas públicas; ambas com limite de requisições por IP.</summary>
    private static readonly HashSet<string> RotasPublicas =
        ["POST api/auth/login", "POST api/medicos/solicitacoes"];

    private static async Task<JsonElement> ProblemaAsync(HttpResponseMessage resposta)
    {
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json);
    }

    private List<(string Metodo, string Modelo, RouteEndpoint Rota)> Rotas()
        => [.. ambiente.Fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(m => (m, e.RoutePattern.RawText!, e)))];

    /// <summary>Preenche os parâmetros da rota com valores válidos quaisquer.</summary>
    private static string Url(string modelo)
    {
        var url = modelo;
        foreach (var (parametro, valor) in new[] { ("{mao}", "DIREITA"), ("{dedo}", "POLEGAR") })
        {
            url = url.Replace(parametro, valor, StringComparison.Ordinal);
        }

        while (url.IndexOf('{', StringComparison.Ordinal) is var inicio and >= 0)
        {
            url = string.Concat(url.AsSpan(0, inicio), Guid.NewGuid().ToString(), url.AsSpan(url.IndexOf('}', inicio) + 1));
        }

        return "/" + url;
    }

    [Fact]
    public async Task Toda_rota_exige_login_exceto_as_duas_publicas()
    {
        var rotas = Rotas();
        // Rota nova obriga a rever este teste: é o alarme contra rota pública criada por engano.
        Assert.True(rotas.Count == 28, $"A API tem {rotas.Count} rotas; esperadas 28. Revise RotasPublicas e atualize.");
        var anonimo = ambiente.Cliente();

        foreach (var (metodo, modelo, rota) in rotas)
        {
            var chave = $"{metodo} {modelo}";
            if (RotasPublicas.Contains(chave))
            {
                Assert.NotNull(rota.Metadata.GetMetadata<EnableRateLimitingAttribute>());
                continue;
            }

            using var requisicao = new HttpRequestMessage(new HttpMethod(metodo), Url(modelo));
            var resposta = await anonimo.SendAsync(requisicao);

            Assert.True(resposta.StatusCode == HttpStatusCode.Unauthorized, $"{chave} respondeu {(int)resposta.StatusCode} sem login.");
            Assert.Equal("Autenticação necessária. Faça login novamente.", (await ProblemaAsync(resposta)).GetProperty("title").GetString());
        }

        Assert.All(RotasPublicas, publica => Assert.Contains(rotas, r => $"{r.Metodo} {r.Modelo}" == publica));
    }

    [Fact]
    public async Task Papel_errado_recebe_403_em_problem_json()
    {
        var medico = await ambiente.MedicoAprovadoAsync();

        var resposta = await medico.Cliente.GetAsync("/api/medicos/solicitacoes");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Acesso não permitido para o seu perfil.", (await ProblemaAsync(resposta)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Sem_login_rota_inexistente_tambem_responde_401()
    {
        // Quem não está logado não consegue descobrir quais rotas existem.
        var resposta = await ambiente.Cliente().GetAsync("/api/rota-que-nao-existe");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("Autenticação necessária. Faça login novamente.", (await ProblemaAsync(resposta)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Com_login_rota_inexistente_e_metodo_errado_viram_problem_json()
    {
        var medico = await ambiente.MedicoAprovadoAsync();

        var naoExiste = await medico.Cliente.GetAsync("/api/rota-que-nao-existe");
        var metodoErrado = await medico.Cliente.DeleteAsync("/api/pacientes");

        Assert.Equal(HttpStatusCode.NotFound, naoExiste.StatusCode);
        Assert.Equal("Recurso não encontrado.", (await ProblemaAsync(naoExiste)).GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.MethodNotAllowed, metodoErrado.StatusCode);
        Assert.Equal("Método não permitido para esta rota.", (await ProblemaAsync(metodoErrado)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Corpo_malformado_ou_de_outro_tipo_vira_problem_json()
    {
        var malformado = await ambiente.Cliente().PostAsync("/api/auth/login",
            new StringContent("{ \"email\": ", Encoding.UTF8, "application/json"));
        var textoPuro = await ambiente.Cliente().PostAsync("/api/auth/login",
            new StringContent("email=x", Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.BadRequest, malformado.StatusCode);
        Assert.Equal("Dados inválidos", (await ProblemaAsync(malformado)).GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, textoPuro.StatusCode);
        Assert.Equal("Tipo de conteúdo não suportado.", (await ProblemaAsync(textoPuro)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Erros_trazem_trace_id_e_nenhum_detalhe_interno()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        var resposta = await medico.Cliente.GetAsync($"/api/pacientes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        var problema = await ProblemaAsync(resposta);
        Assert.Equal("Paciente não encontrado.", problema.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(problema.GetProperty("traceId").GetString()));
        Assert.False(problema.TryGetProperty("exception", out _));
        Assert.False(problema.TryGetProperty("detail", out var detalhe) && detalhe.ValueKind == JsonValueKind.String
                     && detalhe.GetString()!.Contains("   at ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Respostas_nao_ficam_em_cache_nem_sao_reinterpretadas()
    {
        var medico = await ambiente.MedicoAprovadoAsync();

        foreach (var resposta in new[]
                 {
                     await medico.Cliente.GetAsync("/api/pacientes"),
                     await ambiente.Cliente().GetAsync("/api/pacientes"),
                 })
        {
            Assert.True(resposta.Headers.CacheControl?.NoStore, "Cache-Control: no-store ausente.");
            Assert.Equal("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("no-referrer", resposta.Headers.GetValues("Referrer-Policy").Single());
        }
    }

    [Fact]
    public async Task Cors_libera_so_a_origem_do_front()
    {
        async Task<HttpResponseMessage> PreflightAsync(string origem)
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Options, "/api/pacientes");
            requisicao.Headers.Add("Origin", origem);
            requisicao.Headers.Add("Access-Control-Request-Method", "POST");
            requisicao.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
            return await ambiente.Cliente().SendAsync(requisicao);
        }

        var permitido = await PreflightAsync(AmbienteApi.OrigemFront);
        var outro = await PreflightAsync("https://site-desconhecido.example");

        Assert.Equal(HttpStatusCode.NoContent, permitido.StatusCode);
        Assert.Equal(AmbienteApi.OrigemFront, permitido.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(outro.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_expoe_location_nas_respostas_ao_front()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/pacientes")
        {
            Content = JsonContent.Create(new { nome = "Adulto Sintético", dataNascimento = "1990-01-01", sexo = "MASCULINO" }),
        };
        requisicao.Headers.Add("Origin", AmbienteApi.OrigemFront);

        var resposta = await medico.Cliente.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal(AmbienteApi.OrigemFront, resposta.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("Location", resposta.Headers.GetValues("Access-Control-Expose-Headers").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Limite_de_requisicoes_responde_429_em_problem_json()
    {
        await using var limitada = ambiente.Fabrica.WithWebHostBuilder(host =>
            host.UseSetting("LimiteRequisicoes:EntradaPorMinuto", "2"));
        var cliente = limitada.CreateClient();
        var credenciais = new { email = "ninguem@teste.local", senha = "qualquersenha1" };

        var respostas = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++)
        {
            respostas.Add(await cliente.PostAsJsonAsync("/api/auth/login", credenciais));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, respostas[0].StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, respostas[1].StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, respostas[2].StatusCode);
        Assert.Equal("Muitas tentativas. Aguarde um minuto e tente novamente.",
            (await ProblemaAsync(respostas[2])).GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://localhost:5173/")]
    [InlineData("http://localhost:5173/app")]
    [InlineData("localhost:5173")]
    public void Origem_de_cors_invalida_impede_a_api_de_subir(string origem)
    {
        using var invalida = ambiente.Fabrica.WithWebHostBuilder(host =>
            host.UseSetting("Cors:OrigensPermitidas:0", origem));

        var erro = Assert.ThrowsAny<Exception>(() => invalida.Server);

        Assert.Contains("Cors:OrigensPermitidas", (erro.InnerException ?? erro).Message, StringComparison.Ordinal);
    }
}
