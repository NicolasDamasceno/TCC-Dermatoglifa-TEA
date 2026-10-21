using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Infraestrutura.Auditoria;
using Dermatoglifia.Infraestrutura.Identidade;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Identity;
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
        services.AddScoped<IContextoDados>(sp => sp.GetRequiredService<AppDbContext>());

        // ADR-0006: Identity apenas para contas, senha e bloqueio; o token é JWT próprio.
        services
            .AddIdentityCore<Usuario>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddErrorDescriber<DescritorErrosIdentity>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddSingleton<IRelogio, RelogioSistema>();
        services.AddScoped<IContas, Contas>();
        services.AddScoped<IAuditoria, AuditoriaEf>();
        return services;
    }

    public static void ConfigurarBanco(DbContextOptionsBuilder options, string connectionString)
        => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__historico_migracoes"))
            .UseSnakeCaseNamingConvention();
}

internal sealed class RelogioSistema : IRelogio
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}
