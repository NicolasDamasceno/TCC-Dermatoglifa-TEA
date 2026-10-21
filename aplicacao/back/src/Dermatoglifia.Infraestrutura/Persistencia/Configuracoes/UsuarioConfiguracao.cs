using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Infraestrutura.Identidade;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dermatoglifia.Infraestrutura.Persistencia.Configuracoes;

internal sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario", t =>
        {
            t.HasCheckConstraint("ck_usuario_papel", EnumComoTexto.Check<PapelUsuario>("papel"));
            t.HasCheckConstraint("ck_usuario_status", EnumComoTexto.Check<StatusCadastro>("status"));
        });

        // Colunas listadas no doc 05 com nomes em português; as demais do
        // Identity (telefone, 2FA etc.) existem mas não são usadas no MVP.
        builder.Property(u => u.Nome).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasColumnName("email_normalizado").HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("senha_hash");
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
    }
}

internal sealed class TabelasIdentityConfiguracao :
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("usuario_claim");

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("usuario_login");

    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("usuario_token");
}

internal sealed class MedicoConfiguracao : IEntityTypeConfiguration<Medico>
{
    public void Configure(EntityTypeBuilder<Medico> builder)
    {
        builder.ToTable("medico");
        builder.HasKey(m => m.UsuarioId);
        builder.Property(m => m.UsuarioId).ValueGeneratedNever();

        builder.Property(m => m.Crm).HasMaxLength(10).IsRequired();
        builder.Property(m => m.UfCrm).HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(m => m.Especialidade).HasMaxLength(100).IsRequired();
        builder.Property(m => m.VinculoProtocolo).HasMaxLength(200);
        builder.Property(m => m.MotivoRecusa).HasMaxLength(300);

        builder.HasIndex(m => new { m.Crm, m.UfCrm }).IsUnique();

        builder.HasOne<Usuario>().WithOne().HasForeignKey<Medico>(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(m => m.AprovadoPor).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(m => m.Aprovado);
        builder.Ignore(m => m.Recusado);
        builder.Ignore(m => m.Pendente);
    }
}

internal sealed class RegistroAuditoriaConfiguracao : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("log_auditoria");
        builder.Property(r => r.Id).UseIdentityAlwaysColumn();
        builder.Property(r => r.Acao).HasMaxLength(60).IsRequired();
        builder.Property(r => r.Entidade).HasMaxLength(60).IsRequired();
        builder.Property(r => r.Detalhes).HasColumnType("jsonb");
        builder.HasIndex(r => r.OcorridoEm);
        builder.HasIndex(r => new { r.Entidade, r.EntidadeId });

        builder.HasOne<Usuario>().WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
