using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Infraestrutura;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

    /// <summary>Origem do front aceita pelo CORS nos testes.</summary>
    public const string OrigemFront = "http://localhost:5173";

    /// <summary>Modelo sintético ativo cadastrado no banco de testes.</summary>
    public const string VersaoModeloTeste = "teste_sintetico_v1";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    private PostgreSqlContainer? _conteiner;
    private string _servidor = null!;
    private string _nomeBanco = null!;

    public WebApplicationFactory<Program> Fabrica { get; private set; } = null!;

    /// <summary>Serviço de IA falso: recebe as chamadas HTTP no lugar do serviço Python.</summary>
    public IaFalsa Ia { get; } = new();

    /// <summary>Pasta temporária das imagens cifradas desta execução (apagada no fim).</summary>
    public string DiretorioImagens { get; } = Path.Combine(Path.GetTempPath(), $"dermatoglifia-testes-{Guid.NewGuid():N}");

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

            var modelo = ModeloIA.Registrar(VersaoModeloTeste, "CNN 2 blocos (teste)", OrigemDados.Sintetico,
                escoreValidado: false, new string('a', 64), "{\"acuraciaBalanceada\": 0.9}", new DateOnly(2026, 11, 1));
            modelo.Ativar(DateTimeOffset.UtcNow);
            contexto.ModelosIA.Add(modelo);
            await contexto.SaveChangesAsync();
        }
        var configuracao = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Banco"] = banco,
            ["Jwt:Chave"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["LimiteRequisicoes:EntradaPorMinuto"] = "10000",
            ["AdminInicial:Email"] = EmailAdmin,
            ["AdminInicial:Senha"] = SenhaAdmin,
            ["Armazenamento:Diretorio"] = DiretorioImagens,
            ["Armazenamento:Chaves:1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Inferencia:UrlBase"] = "http://ia-falsa.teste/",
            ["Inferencia:TimeoutSegundos"] = "2",
            ["Cors:OrigensPermitidas:0"] = OrigemFront,
        };

        Fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testes");
            foreach (var (chave, valor) in configuracao)
            {
                host.UseSetting(chave, valor);
            }

            // Nenhuma chamada sai da máquina: o HttpClient do Serviço de IA fala com a IA falsa.
            host.ConfigureTestServices(servicos => servicos
                .AddHttpClient(InjecaoDeDependencia.NomeClienteInferencia)
                .ConfigurePrimaryHttpMessageHandler(() => new HandlerIaFalsa(Ia)));
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

        if (Directory.Exists(DiretorioImagens))
        {
            Directory.Delete(DiretorioImagens, recursive: true);
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

    /// <summary>Cria um médico sintético, aprova pelo admin e devolve um cliente já autenticado.</summary>
    public async Task<MedicoDeTeste> MedicoAprovadoAsync()
    {
        const string senha = "senhamedico123";
        var email = $"medico-{Guid.NewGuid():N}@teste.local";
        var crm = Random.Shared.Next(100_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var solicitacao = await Cliente().PostAsJsonAsync("/api/medicos/solicitacoes",
            new { nome = "Médico Sintético", email, senha, crm, ufCrm = "PI", especialidade = "Pediatria" }, Json);
        solicitacao.EnsureSuccessStatusCode();
        var id = (await solicitacao.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var admin = Cliente(await TokenAsync(EmailAdmin, SenhaAdmin));
        (await admin.PostAsJsonAsync($"/api/medicos/{id}/aprovacao", new { vinculoProtocolo = "Equipe sintética" })).EnsureSuccessStatusCode();

        return new MedicoDeTeste(id, email, Cliente(await TokenAsync(email, senha)));
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

public sealed record MedicoDeTeste(Guid Id, string Email, HttpClient Cliente);
