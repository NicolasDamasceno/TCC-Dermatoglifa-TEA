using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dermatoglifia.Infraestrutura.Persistencia;

/// <summary>
/// Usada apenas pelas ferramentas do EF Core (<c>dotnet ef</c>) para criar
/// migrations. A connection string vem de <c>ConnectionStrings__Banco</c>; sem
/// ela, usa um banco local apenas para gerar os arquivos (nenhuma conexão é
/// aberta ao criar uma migration).
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Banco")
            ?? "Host=localhost;Database=dermatoglifia;Username=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        InjecaoDeDependencia.ConfigurarBanco(options, connectionString);
        return new AppDbContext(options.Options);
    }
}
