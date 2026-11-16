using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class MicroareaConfiguracao : IEntityTypeConfiguration<Microarea>
{
    public void Configure(EntityTypeBuilder<Microarea> b)
    {
        b.ToTable("Microareas");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).ValueGeneratedNever();
        b.Property(m => m.Nome).HasMaxLength(120).IsRequired();
        b.HasIndex(m => m.Nome).IsUnique();
    }
}
