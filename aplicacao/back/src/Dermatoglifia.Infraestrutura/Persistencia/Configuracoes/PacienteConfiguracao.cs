using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Infraestrutura.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dermatoglifia.Infraestrutura.Persistencia.Configuracoes;

internal sealed class PacienteConfiguracao : IEntityTypeConfiguration<Paciente>
{
    public void Configure(EntityTypeBuilder<Paciente> builder)
    {
        builder.ToTable("paciente", t =>
        {
            t.HasCheckConstraint("ck_paciente_sexo", EnumComoTexto.Check<SexoBiologico>("sexo"));
            t.HasCheckConstraint("ck_paciente_situacao_diagnostica",
                EnumComoTexto.Check<SituacaoDiagnostica>("situacao_diagnostica"));
        });
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Situacao).HasColumnName("situacao_diagnostica");
        builder.Property(p => p.FonteDiagnostico).HasMaxLength(200);

        builder.HasOne<Medico>().WithMany().HasForeignKey(p => p.MedicoResponsavelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.MedicoResponsavelId);

        // Identificação em schema separado; removida quando o titular pede eliminação.
        builder.HasOne(p => p.Identificacao)
            .WithOne()
            .HasForeignKey<IdentificacaoPaciente>(i => i.PacienteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Consentimentos)
            .WithOne()
            .HasForeignKey(c => c.PacienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(p => p.Anonimizado);
        builder.Ignore(p => p.PodeColetar);
        builder.Ignore(p => p.AutorizaUsoEmTreino);
    }
}

internal sealed class IdentificacaoPacienteConfiguracao : IEntityTypeConfiguration<IdentificacaoPaciente>
{
    public void Configure(EntityTypeBuilder<IdentificacaoPaciente> builder)
    {
        builder.ToTable("identificacao_paciente", AppDbContext.SchemaIdentificacao);
        builder.HasKey(i => i.PacienteId);
        builder.Property(i => i.PacienteId).ValueGeneratedNever();

        builder.Property(i => i.Nome).HasMaxLength(150).IsRequired();
        builder.Property(i => i.ResponsavelNome).HasMaxLength(150);
        builder.Property(i => i.ResponsavelParentesco).HasMaxLength(50);
        builder.Property(i => i.ResponsavelContato).HasMaxLength(100);
    }
}

internal sealed class ConsentimentoConfiguracao : IEntityTypeConfiguration<Consentimento>
{
    public void Configure(EntityTypeBuilder<Consentimento> builder)
    {
        builder.ToTable("consentimento", t =>
            t.HasCheckConstraint("ck_consentimento_tipo", EnumComoTexto.Check<TipoTermo>("tipo")));
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.VersaoTermo).HasMaxLength(50).IsRequired();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(c => c.RegistradoPor).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(c => c.EstaVigente);
        builder.Ignore(c => c.LiberaColeta);
    }
}
