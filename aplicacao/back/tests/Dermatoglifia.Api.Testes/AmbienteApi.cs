using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dermatoglifia.Infraestrutura;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Dermatoglifia.Api.Testes;

/// <summary>
/// Sobe a API inteira contra um PostgreSQL real, num banco criado só para a
/// execução dos testes e apagado no fim.
/// <para>
/// Por padrão usa um contêiner (Testcontainers — exige Docker). Se a variável
/// <c>TESTES_BANCO</c> tiver uma connection string, usa esse servidor.
/// </para>
/// </summary>
public sealed class AmbienteApi : IAsyncLifetime
{
    public const string EmailAdmin = "admin@teste.local";
    public const string SenhaAdmin = "senhaadmin123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    private PostgreSqlContainer? _conteiner;
    private string _servidor = null!;
    private string _nomeBanco = null!;

    public WebApplicationFactory<Program> Fabrica { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _servidor = Environment.GetEnvironmentVariable("TESTES_BANCO") ?? await IniciarConteinerAsync();
        _nomeBanco = $"testes_{Guid.NewGuid():N}";
        await ExecutarNoServidorAsync($"CREATE DATABASE \"{_nomeBanco}\"");

        var banco = new NpgsqlConnectionStringBuilder(_servidor) { Database = _nomeBanco }.ConnectionString;

        // As migrations precisam estar aplicadas antes de a API subir (ela cria o admin inicial na partida).
        var opcoes = new DbContextOptionsBuilder<AppDbContext>();
        InjecaoDeDependencia.ConfigurarBanco(opcoes, banco);
        await using (var contexto = new AppDbContext(opcoes.Options))
        {
            await contexto.Database.MigrateAsync();
        }
        var configuracao = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Banco"] = banco,
            ["Jwt:Chave"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["LimiteRequisicoes:EntradaPorMinuto"] = "10000",
            ["AdminInicial:Email"] = EmailAdmin,
            ["AdminInicial:Senha"] = SenhaAdmin,
        };

        Fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testes");
            foreach (var (chave, valor) in configuracao)
            {
                host.UseSetting(chave, valor);
            }
        });

        _ = Fabrica.Server; // sobe a API agora: falhas de configuração aparecem aqui
    }

    public async Task DisposeAsync()
    {
        await Fabrica.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await ExecutarNoServidorAsync($"DROP DATABASE IF EXISTS \"{_nomeBanco}\" WITH (FORCE)");
        if (_conteiner is not null)
        {
            await _conteiner.DisposeAsync();
        }
    }

    public HttpClient Cliente(string? token = null)
    {
        var cliente = Fabrica.CreateClient();
        if (token is not null)
        {
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return cliente;
    }

    public async Task<T> NoEscopoAsync<T>(Func<IServiceProvider, Task<T>> acao)
    {
        await using var escopo = Fabrica.Services.CreateAsyncScope();
        return await acao(escopo.ServiceProvider);
    }

    public Task<HttpResponseMessage> EntrarAsync(string email, string senha)
        => Cliente().PostAsJsonAsync("/api/auth/login", new { email, senha }, Json);

    public async Task<string> TokenAsync(string email, string senha)
    {
        var resposta = await EntrarAsync(email, senha);
        resposta.EnsureSuccessStatusCode();
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        return corpo.GetProperty("token").GetString()!;
    }

    private async Task<string> IniciarConteinerAsync()
    {
        _conteiner = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await _conteiner.StartAsync();
        return _conteiner.GetConnectionString();
    }

    private async Task ExecutarNoServidorAsync(string sql)
    {
        var administrativo = new NpgsqlConnectionStringBuilder(_servidor) { Database = "postgres" }.ConnectionString;
        await using var conexao = new NpgsqlConnection(administrativo);
        await conexao.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Nome)]
public sealed class ColecaoApi : ICollectionFixture<AmbienteApi>
{
    public const string Nome = "API com PostgreSQL";
}
