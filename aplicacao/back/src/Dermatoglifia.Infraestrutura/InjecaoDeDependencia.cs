using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dermatoglifia.Infraestrutura;

public static class InjecaoDeDependencia
{
    public const string NomeConnectionString = "Banco";

    /// <summary>Registra os serviços da Infraestrutura. Falha na hora se faltar a connection string.</summary>
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(NomeConnectionString);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string \"{NomeConnectionString}\" não configurada. " +
                "Use 'dotnet user-secrets' em desenvolvimento ou a variável ConnectionStrings__Banco.");
        }

        services.AddDbContext<AppDbContext>(options => ConfigurarBanco(options, connectionString));
        return services;
    }

    internal static void ConfigurarBanco(DbContextOptionsBuilder options, string connectionString)
        => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__historico_migracoes"))
            .UseSnakeCaseNamingConvention();
}
