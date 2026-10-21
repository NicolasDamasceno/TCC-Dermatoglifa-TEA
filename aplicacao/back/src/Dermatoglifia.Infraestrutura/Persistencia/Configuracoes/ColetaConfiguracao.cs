using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dermatoglifia.Infraestrutura.Persistencia.Configuracoes;

internal sealed class SessaoColetaConfiguracao : IEntityTypeConfiguration<SessaoColeta>
{
    public void Configure(EntityTypeBuilder<SessaoColeta> builder)
    {
        builder.ToTable("sessao_coleta", t =>
        {
            t.HasCheckConstraint("ck_sessao_coleta_metodo", EnumComoTexto.Check<MetodoCaptura>("metodo"));
            t.HasCheckConstraint("ck_sessao_coleta_tipo_impressao", EnumComoTexto.Check<TipoImpressao>("tipo_impressao"));
            t.HasCheckConstraint("ck_sessao_coleta_status", EnumComoTexto.Check<StatusSessao>("status"));
            t.HasCheckConstraint("ck_sessao_coleta_idade_meses", "\"idade_meses\" >= 0");
        });
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Dispositivo).HasMaxLength(100);
        builder.Property(s => s.Observacoes).HasMaxLength(1000);

        // Concorrência otimista pela coluna de sistema xmin do PostgreSQL.
        builder.Property(s => s.Versao).IsRowVersion();

        builder.HasOne<Paciente>().WithMany().HasForeignKey(s => s.PacienteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Medico>().WithMany().HasForeignKey(s => s.MedicoId).OnDelete(DeleteBehavior.Restrict);

        // No máximo uma sessão em andamento por paciente.
        builder.HasIndex(s => s.PacienteId)
            .IsUnique()
            .HasFilter($"\"status\" = '{EnumComoTexto.ParaTexto(StatusSessao.EmAndamento)}'")
            .HasDatabaseName("ux_sessao_coleta_em_andamento_por_paciente");

        builder.HasMany(s => s.Digitais)
            .WithOne()
            .HasForeignKey(d => d.SessaoColetaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DigitalConfiguracao : IEntityTypeConfiguration<Digital>
{
    public void Configure(EntityTypeBuilder<Digital> builder)
    {
        builder.ToTable("digital", t =>
        {
            t.HasCheckConstraint("ck_digital_mao", EnumComoTexto.Check<Mao>("mao"));
            t.HasCheckConstraint("ck_digital_dedo", EnumComoTexto.Check<Dedo>("dedo"));
            t.HasCheckConstraint("ck_digital_motivo_nao_coletavel",
                "\"coletavel\" OR \"motivo_nao_coletavel\" IS NOT NULL");
            t.HasCheckConstraint("ck_digital_arquivo_quando_coletavel",
                "NOT \"coletavel\" OR (\"caminho_arquivo\" IS NOT NULL AND \"hash_sha256\" IS NOT NULL)");
            t.HasCheckConstraint("ck_digital_resolucao_minima", "\"resolucao_dpi\" IS NULL OR \"resolucao_dpi\" >= 500");
            t.HasCheckConstraint("ck_digital_nfiq2", "\"nfiq2\" IS NULL OR \"nfiq2\" BETWEEN 0 AND 100");
        });
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.MotivoNaoColetavel).HasMaxLength(200);
        builder.Property(d => d.CaminhoArquivo).HasMaxLength(300);
        builder.Property(d => d.HashSha256).HasMaxLength(64).IsFixedLength();
        builder.Property(d => d.Formato).HasMaxLength(10);

        // Uma imagem vigente por dedo em cada sessão.
        builder.HasIndex(d => new { d.SessaoColetaId, d.Mao, d.Dedo }).IsUnique();

        builder.Ignore(d => d.Posicao);
    }
}
