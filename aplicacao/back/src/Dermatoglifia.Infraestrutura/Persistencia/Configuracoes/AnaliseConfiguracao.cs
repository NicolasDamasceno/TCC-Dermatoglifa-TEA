using System.Text.Json;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dermatoglifia.Infraestrutura.Persistencia.Configuracoes;

internal sealed class ModeloIAConfiguracao : IEntityTypeConfiguration<ModeloIA>
{
    public void Configure(EntityTypeBuilder<ModeloIA> builder)
    {
        builder.ToTable("modelo_ia", t =>
            t.HasCheckConstraint("ck_modelo_ia_origem_dados", EnumComoTexto.Check<OrigemDados>("origem_dados")));
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Versao).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Arquitetura).HasMaxLength(100).IsRequired();
        builder.Property(m => m.HashSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(m => m.MetricasTeste).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(m => m.Versao).IsUnique();

        // Apenas um modelo ativo por vez.
        builder.HasIndex(m => m.Ativo)
            .IsUnique()
            .HasFilter("\"ativo\"")
            .HasDatabaseName("ux_modelo_ia_um_ativo");
    }
}

internal sealed class AnaliseConfiguracao : IEntityTypeConfiguration<Analise>
{
    public void Configure(EntityTypeBuilder<Analise> builder)
    {
        builder.ToTable("analise", t =>
        {
            t.HasCheckConstraint("ck_analise_status", EnumComoTexto.Check<StatusAnalise>("status"));
            t.HasCheckConstraint("ck_analise_escore_risco", "\"escore_risco\" IS NULL OR \"escore_risco\" BETWEEN 0 AND 1");
            t.HasCheckConstraint("ck_analise_discordancia",
                "\"discordancia_bilateral\" IS NULL OR (\"discordancia_bilateral\" BETWEEN 0 AND 4 " +
                "AND \"discordancia_bilateral_com_polegar\" BETWEEN 0 AND 5)");
        });
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.EscoreRisco).HasPrecision(5, 4);
        builder.Property(a => a.VersaoAviso).HasMaxLength(20).IsRequired();
        builder.Property(a => a.ObservacaoMedico).HasMaxLength(2000);
        builder.Property(a => a.MensagemErro).HasMaxLength(500);
        builder.Property(a => a.Versao).IsRowVersion();

        // Indicadores ficam como colunas da própria tabela (nulas até concluir).
        builder.ComplexProperty(a => a.Indicadores, indicadores =>
        {
            indicadores.Property(i => i.QtdArcos).HasColumnName("qtd_arcos");
            indicadores.Property(i => i.QtdArcosTendidos).HasColumnName("qtd_arcos_tendidos");
            indicadores.Property(i => i.QtdAlcasUlnares).HasColumnName("qtd_alcas_ulnares");
            indicadores.Property(i => i.QtdAlcasRadiais).HasColumnName("qtd_alcas_radiais");
            indicadores.Property(i => i.QtdVerticilos).HasColumnName("qtd_verticilos");
            indicadores.Property(i => i.DiscordanciaBilateral).HasColumnName("discordancia_bilateral");
            indicadores.Property(i => i.DiscordanciaBilateralComPolegar).HasColumnName("discordancia_bilateral_com_polegar");
        });

        builder.HasOne<SessaoColeta>().WithOne().HasForeignKey<Analise>(a => a.SessaoColetaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ModeloIA>().WithMany().HasForeignKey(a => a.ModeloIAId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Resultados)
            .WithOne()
            .HasForeignKey(r => r.AnaliseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ResultadoDigitalConfiguracao : IEntityTypeConfiguration<ResultadoDigital>
{
    public void Configure(EntityTypeBuilder<ResultadoDigital> builder)
    {
        builder.ToTable("resultado_digital", t =>
        {
            t.HasCheckConstraint("ck_resultado_digital_classe_imagem", EnumComoTexto.Check<ClasseImagem>("classe_imagem"));
            t.HasCheckConstraint("ck_resultado_digital_padrao_previsto", EnumComoTexto.Check<PadraoDigital>("padrao_previsto"));
            t.HasCheckConstraint("ck_resultado_digital_confianca", "\"confianca\" BETWEEN 0 AND 1");
        });
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Confianca).HasPrecision(5, 4);
        builder.Property(r => r.Probabilidades)
            .HasColumnType("jsonb")
            .HasConversion(
                p => SerializarProbabilidades(p),
                json => DesserializarProbabilidades(json),
                new ValueComparer<IReadOnlyDictionary<ClasseImagem, decimal>>(
                    (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
                    p => p.Aggregate(0, (hash, par) => HashCode.Combine(hash, par.Key, par.Value)),
                    p => new Dictionary<ClasseImagem, decimal>(p)));

        builder.HasOne<Digital>().WithOne().HasForeignKey<ResultadoDigital>(r => r.DigitalId).OnDelete(DeleteBehavior.Restrict);
    }

    // Chaves no mesmo formato da API e do banco: {"ARCO": 0.05, "VERTICILO": 0.8, ...}
    private static string SerializarProbabilidades(IReadOnlyDictionary<ClasseImagem, decimal> probabilidades)
        => JsonSerializer.Serialize(probabilidades.ToDictionary(p => EnumComoTexto.ParaTexto(p.Key), p => p.Value));

    private static Dictionary<ClasseImagem, decimal> DesserializarProbabilidades(string json)
        => (JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? [])
            .ToDictionary(p => EnumComoTexto.DeTexto<ClasseImagem>(p.Key), p => p.Value);
}
