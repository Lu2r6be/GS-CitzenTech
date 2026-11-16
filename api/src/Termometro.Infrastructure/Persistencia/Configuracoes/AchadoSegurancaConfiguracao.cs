using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class AchadoSegurancaConfiguracao : IEntityTypeConfiguration<AchadoSeguranca>
{
    public void Configure(EntityTypeBuilder<AchadoSeguranca> b)
    {
        b.ToTable("Achados");
        b.HasKey(a => a.Id);
        b.Property(a => a.Tipo).HasMaxLength(100).IsRequired();
        b.Property(a => a.Origem).HasMaxLength(200).IsRequired();
        b.Property(a => a.Severidade).HasConversion<string>().HasMaxLength(10);
        b.Property(a => a.Exposicao).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.CategoriaPii).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(a => a.CriadoEm);
    }
}
