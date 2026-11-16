using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class DispositivoConfiguracao : IEntityTypeConfiguration<Dispositivo>
{
    public void Configure(EntityTypeBuilder<Dispositivo> b)
    {
        b.ToTable("Dispositivos");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).HasMaxLength(50).ValueGeneratedNever();
        b.Property(d => d.DeviceKeyHash).HasMaxLength(200).IsRequired();
        b.HasOne<Domicilio>().WithMany().HasForeignKey(d => d.DomicilioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => d.DomicilioId);
    }
}
