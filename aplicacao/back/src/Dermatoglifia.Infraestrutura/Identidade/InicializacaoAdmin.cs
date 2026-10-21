using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dermatoglifia.Infraestrutura.Identidade;

public static partial class InicializacaoAdmin
{
    /// <summary>
    /// Cria o primeiro administrador a partir de <c>AdminInicial:Email</c> e
    /// <c>AdminInicial:Senha</c> (somente variáveis de ambiente — nunca no código
    /// ou no Git), se ainda não existir nenhum administrador.
    /// </summary>
    public static async Task CriarAdminInicialAsync(this IServiceProvider servicos, IConfiguration configuracao)
    {
        var email = configuracao["AdminInicial:Email"];
        var senha = configuracao["AdminInicial:Senha"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            return;
        }

        await using var escopo = servicos.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(InicializacaoAdmin));

        if ((await contexto.Database.GetPendingMigrationsAsync()).Any())
        {
            throw new InvalidOperationException(
                "O banco tem migrations pendentes. Rode 'dotnet ef database update " +
                "-p src/Dermatoglifia.Infraestrutura -s src/Dermatoglifia.Api' antes de iniciar a API.");
        }

        if (await contexto.Users.AnyAsync(u => u.Papel == PapelUsuario.Admin))
        {
            return;
        }

        var relogio = escopo.ServiceProvider.GetRequiredService<IRelogio>();
        var userManager = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var nome = configuracao["AdminInicial:Nome"] ?? "Administrador";
        var admin = Usuario.Criar(nome, email, PapelUsuario.Admin, relogio.Agora);

        var resultado = await userManager.CreateAsync(admin, senha);
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(
                "Não foi possível criar o administrador inicial: " +
                string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }

        escopo.ServiceProvider.GetRequiredService<IAuditoria>()
            .Registrar("ADMIN_INICIAL_CRIADO", "usuario", admin.Id, usuarioId: admin.Id);
        await contexto.SaveChangesAsync();
        LogAdminCriado(logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Administrador inicial criado.")]
    private static partial void LogAdminCriado(ILogger logger);
}
