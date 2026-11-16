using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class DiaRiscoConfiguracao : IEntityTypeConfiguration<DiaRisco>
{
    public void Configure(EntityTypeBuilder<DiaRisco> b)
    {
        b.ToTable("DiasRisco");
        b.HasKey(d => d.Id);
        b.Property(d => d.Estacao).HasMaxLength(10).IsRequired();
        b.Property(d => d.Tipo).HasConversion<string>().HasMaxLength(10);
        b.Property(d => d.ModeloVersao).HasMaxLength(50).IsRequired();
        b.HasIndex(d => new { d.Data, d.Estacao }).IsUnique();
    }
}
