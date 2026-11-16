using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).ValueGeneratedNever();
        b.Property(u => u.Nome).HasMaxLength(200).IsRequired();
        b.Property(u => u.Email).HasMaxLength(254).IsRequired();
        b.Property(u => u.SenhaHash).HasMaxLength(200).IsRequired();
        b.Property(u => u.Papel).HasConversion<string>().HasMaxLength(10);
        b.HasIndex(u => u.Email).IsUnique();

        b.HasOne<Microarea>().WithMany().HasForeignKey(u => u.MicroareaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Residente>().WithMany().HasForeignKey(u => u.ResidenteId).OnDelete(DeleteBehavior.Restrict);
    }
}
