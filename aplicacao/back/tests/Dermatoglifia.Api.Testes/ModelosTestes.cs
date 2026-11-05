using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dermatoglifia.Api.Testes;

/// <summary>UC10 de ponta a ponta, com a IA falsa e versões sintéticas.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ModelosTestes : IDisposable
{
    private const string NovaVersao = "2026-11-05_promocao_teste_v2";
    private static readonly string NovoHash = new('c', 64);
    private readonly AmbienteApi _ambiente;

    public ModelosTestes(AmbienteApi ambiente)
    {
        _ambiente = ambiente;
        _ambiente.Ia.Reiniciar();
    }

    // Os outros testes dependem do modelo de teste ativo: ele volta a ser o único ativo.
    public void Dispose()
    {
        _ambiente.Ia.Reiniciar();
        _ambiente.NoEscopoAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<AppDbContext>();
            foreach (var ativo in await contexto.ModelosIA.Where(m => m.Ativo).ToListAsync())
            {
                ativo.Desativar();
            }

            await contexto.SaveChangesAsync();
            (await contexto.ModelosIA.SingleAsync(m => m.Versao == AmbienteApi.VersaoModeloTeste)).Ativar(DateTimeOffset.UtcNow);
            return await contexto.SaveChangesAsync();
        }).GetAwaiter().GetResult();
    }

    private static string Config(string versao, string origem = "SINTETICO") => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["versao"] = versao,
        ["origem_dados"] = origem,
        ["arquitetura"] = "CNN 3 blocos (teste)",
        ["treinado_em"] = "2026-11-05",
        ["classes"] = new[] { "ALCA_DIREITA", "ALCA_ESQUERDA", "ARCO", "ARCO_TENDIDO", "VERTICILO" },
    });

    private const string Metricas =
        """{"teste": {"imagens": 570, "acuracia": 0.99123, "acuracia_balanceada": 0.98766, "auc_macro_um_contra_todos": 1.0}}""";

    private static MultipartFormDataContent Arquivos(string config, string metricas = Metricas)
    {
        var conteudo = new MultipartFormDataContent
        {
            { new StringContent(config, Encoding.UTF8, "application/json"), "config", "config.json" },
            { new StringContent(metricas, Encoding.UTF8, "application/json"), "metricas", "metricas.json" },
        };
        return conteudo;
    }

    private async Task<HttpClient> AdminAsync()
        => _ambiente.Cliente(await _ambiente.TokenAsync(AmbienteApi.EmailAdmin, AmbienteApi.SenhaAdmin));

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage resposta)
        => await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json);

    [Fact]
    public async Task Painel_mostra_o_modelo_do_servico_e_as_versoes_registradas()
    {
        var admin = await AdminAsync();

        var painel = await admin.GetFromJsonAsync<JsonElement>("/api/modelos", AmbienteApi.Json);

        var servico = painel.GetProperty("servico");
        Assert.True(servico.GetProperty("disponivel").GetBoolean());
        Assert.Equal(AmbienteApi.VersaoModeloTeste, servico.GetProperty("versaoModelo").GetString());
        Assert.True(servico.GetProperty("registrado").GetBoolean());
        Assert.True(servico.GetProperty("ativo").GetBoolean());
        Assert.True(servico.GetProperty("hashConfere").GetBoolean());
        var ativo = painel.GetProperty("modelos").EnumerateArray().First();
        Assert.Equal(AmbienteApi.VersaoModeloTeste, ativo.GetProperty("versao").GetString());
        Assert.True(ativo.GetProperty("ativo").GetBoolean());
        Assert.Equal(0.9, ativo.GetProperty("metricas").GetProperty("acuraciaBalanceada").GetDouble());
    }

    [Fact]
    public async Task Promover_registra_a_versao_do_servico_e_desativa_a_anterior()
    {
        var admin = await AdminAsync();
        _ambiente.Ia.Versao = NovaVersao;
        _ambiente.Ia.Sha256 = NovoHash;

        var antes = await admin.GetFromJsonAsync<JsonElement>("/api/modelos", AmbienteApi.Json);
        Assert.False(antes.GetProperty("servico").GetProperty("registrado").GetBoolean());

        var resposta = await admin.PostAsync("/api/modelos/promocao", Arquivos(Config(NovaVersao)));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var modelo = await JsonAsync(resposta);
        Assert.Equal(NovaVersao, modelo.GetProperty("versao").GetString());
        Assert.Equal(NovoHash, modelo.GetProperty("hashSha256").GetString());
        Assert.False(modelo.GetProperty("escoreValidado").GetBoolean());
        Assert.Equal(0.9877, modelo.GetProperty("metricas").GetProperty("acuraciaBalanceada").GetDouble());
        Assert.Equal(570, modelo.GetProperty("metricas").GetProperty("nTeste").GetInt32());

        var ativos = await _ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().ModelosIA
            .Where(m => m.Ativo).Select(m => m.Versao).ToListAsync());
        Assert.Equal([NovaVersao], ativos);

        var auditoria = await _ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().Auditoria
            .Where(a => a.Acao == "MODELO_PROMOVIDO").OrderByDescending(a => a.Id).Select(a => a.Detalhes).FirstAsync());
        Assert.Contains(NovaVersao, auditoria, StringComparison.Ordinal);

        // Voltar ao modelo anterior reativa a versão já registrada (o hash precisa ser o mesmo).
        _ambiente.Ia.Reiniciar();
        var volta = await admin.PostAsync("/api/modelos/promocao", Arquivos(Config(AmbienteApi.VersaoModeloTeste)));
        Assert.Equal(HttpStatusCode.OK, volta.StatusCode);
        Assert.True((await JsonAsync(volta)).GetProperty("ativo").GetBoolean());
    }

    [Fact]
    public async Task Promocao_e_recusada_quando_nao_confere_com_o_servico()
    {
        var admin = await AdminAsync();

        async Task<HttpStatusCode> PromoverAsync(string config, string metricas = Metricas)
            => (await admin.PostAsync("/api/modelos/promocao", Arquivos(config, metricas))).StatusCode;

        // Versão do config diferente da que o serviço carregou
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await PromoverAsync(Config("outra_versao_v9")));
        // Já é o modelo ativo
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await PromoverAsync(Config(AmbienteApi.VersaoModeloTeste)));
        // Mesma versão registrada com outro arquivo
        _ambiente.Ia.Sha256 = new string('d', 64);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await PromoverAsync(Config(AmbienteApi.VersaoModeloTeste)));
        _ambiente.Ia.Reiniciar();
        // Origem dos dados diferente
        _ambiente.Ia.Versao = NovaVersao;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await PromoverAsync(Config(NovaVersao, "REAL")));
        // JSON inválido, campo ausente ou versão com caracteres proibidos
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await PromoverAsync("não é json"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await PromoverAsync(Config(NovaVersao), """{"teste": {}}"""));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await PromoverAsync(Config("x'; DROP TABLE modelo_ia; --")));
        // Serviço sem modelo ou fora do ar
        _ambiente.Ia.MotivoSemModelo = "hash não confere";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await PromoverAsync(Config(NovaVersao)));
        _ambiente.Ia.Responder = (_, _) => throw new HttpRequestException("fora do ar");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, await PromoverAsync(Config(NovaVersao)));

        var ativos = await _ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().ModelosIA
            .Where(m => m.Ativo).Select(m => m.Versao).ToListAsync());
        Assert.Equal([AmbienteApi.VersaoModeloTeste], ativos);
    }

    [Fact]
    public async Task Painel_continua_disponivel_com_o_servico_fora_do_ar()
    {
        var admin = await AdminAsync();
        _ambiente.Ia.Responder = (_, _) => throw new HttpRequestException("fora do ar");

        var painel = await admin.GetFromJsonAsync<JsonElement>("/api/modelos", AmbienteApi.Json);

        Assert.False(painel.GetProperty("servico").GetProperty("disponivel").GetBoolean());
        Assert.Contains("indisponível", painel.GetProperty("servico").GetProperty("motivo").GetString(), StringComparison.Ordinal);
        Assert.NotEmpty(painel.GetProperty("modelos").EnumerateArray());
    }

    [Fact]
    public async Task So_o_administrador_acessa()
    {
        var medico = await _ambiente.MedicoAprovadoAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await medico.Cliente.GetAsync("/api/modelos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await medico.Cliente.PostAsync("/api/modelos/promocao", Arquivos(Config(NovaVersao)))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _ambiente.Cliente().GetAsync("/api/modelos")).StatusCode);
    }
}
