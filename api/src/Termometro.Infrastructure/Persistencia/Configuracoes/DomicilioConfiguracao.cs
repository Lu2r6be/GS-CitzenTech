using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class DomicilioConfiguracao : IEntityTypeConfiguration<Domicilio>
{
    public void Configure(EntityTypeBuilder<Domicilio> b)
    {
        b.ToTable("Domicilios");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).ValueGeneratedNever();
        b.Property(d => d.Endereco).HasMaxLength(300).IsRequired();
        b.Property(d => d.Cep).HasMaxLength(9).IsRequired();
        b.Property(d => d.TipoCobertura).HasConversion<string>().HasMaxLength(20);
        b.Property(d => d.NivelConfirmado).HasConversion<string>().HasMaxLength(10);
        b.Property(d => d.NivelCandidato).HasConversion<string>().HasMaxLength(10);

        b.HasOne<Residente>().WithMany().HasForeignKey(d => d.ResidenteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Microarea>().WithMany().HasForeignKey(d => d.MicroareaId).OnDelete(DeleteBehavior.Restrict);

        // O ACS lista as casas da sua microárea o tempo todo.
        b.HasIndex(d => d.MicroareaId);
        // Um residente tem uma casa monitorada.
        b.HasIndex(d => d.ResidenteId).IsUnique();
    }
}
