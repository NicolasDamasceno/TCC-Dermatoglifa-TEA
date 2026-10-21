using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Infraestrutura.Identidade;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dermatoglifia.Infraestrutura.Persistencia;

/// <summary>
/// Contexto do EF Core (doc 05). Herda de <see cref="IdentityUserContext{TUser, TKey}"/>
/// porque usamos apenas usuários do Identity, sem as tabelas de papéis: o papel é
/// um campo de <see cref="Usuario"/> (ADR-0006).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<Usuario, Guid>(options)
{
    /// <summary>Schema separado para os dados que identificam a pessoa (pseudonimização).</summary>
    public const string SchemaIdentificacao = "identificacao";

    public DbSet<Medico> Medicos => Set<Medico>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<IdentificacaoPaciente> Identificacoes => Set<IdentificacaoPaciente>();
    public DbSet<Consentimento> Consentimentos => Set<Consentimento>();
    public DbSet<SessaoColeta> SessoesColeta => Set<SessaoColeta>();
    public DbSet<Digital> Digitais => Set<Digital>();
    public DbSet<ModeloIA> ModelosIA => Set<ModeloIA>();
    public DbSet<Analise> Analises => Set<Analise>();
    public DbSet<ResultadoDigital> ResultadosDigitais => Set<ResultadoDigital>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        MapearEnum<PapelUsuario>(configurationBuilder);
        MapearEnum<StatusCadastro>(configurationBuilder);
        MapearEnum<SexoBiologico>(configurationBuilder);
        MapearEnum<SituacaoDiagnostica>(configurationBuilder);
        MapearEnum<TipoTermo>(configurationBuilder);
        MapearEnum<MetodoCaptura>(configurationBuilder);
        MapearEnum<TipoImpressao>(configurationBuilder);
        MapearEnum<StatusSessao>(configurationBuilder);
        MapearEnum<Mao>(configurationBuilder);
        MapearEnum<Dedo>(configurationBuilder);
        MapearEnum<StatusAnalise>(configurationBuilder);
        MapearEnum<PadraoDigital>(configurationBuilder);
        MapearEnum<ClasseImagem>(configurationBuilder);
        MapearEnum<OrigemDados>(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // tabelas do Identity
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        NomearRestricoes(builder);
    }

    /// <summary>
    /// Dá nomes coerentes às chaves estrangeiras e aos índices do Identity a partir
    /// dos nomes finais das tabelas (as convenções guardariam nomes antigos, como
    /// "asp_net_users").
    /// </summary>
    private static void NomearRestricoes(ModelBuilder builder)
    {
        foreach (var entidade in builder.Model.GetEntityTypes())
        {
            var tabela = entidade.GetTableName();
            if (tabela is null)
            {
                continue;
            }

            var origem = StoreObjectIdentifier.Table(tabela, entidade.GetSchema());
            string Colunas(IEnumerable<IReadOnlyProperty> propriedades)
                => string.Join("_", propriedades.Select(p => p.GetColumnName(origem)));

            foreach (var chave in entidade.GetForeignKeys())
            {
                var tabelaPrincipal = chave.PrincipalEntityType.GetTableName();
                chave.SetConstraintName($"fk_{tabela}_{tabelaPrincipal}_{Colunas(chave.Properties)}");
            }

            foreach (var indice in entidade.GetIndexes().Where(i => i.GetDatabaseName() is "EmailIndex" or "UserNameIndex"))
            {
                indice.SetDatabaseName($"ix_{tabela}_{Colunas(indice.Properties)}");
            }
        }
    }

    private static void MapearEnum<TEnum>(ModelConfigurationBuilder configurationBuilder)
        where TEnum : struct, Enum
        => configurationBuilder.Properties<TEnum>()
            .HaveConversion<ConversorEnumTexto<TEnum>>()
            .HaveMaxLength(EnumComoTexto.TamanhoMaximo);
}
