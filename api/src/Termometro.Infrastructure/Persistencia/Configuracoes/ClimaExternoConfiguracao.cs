using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class ClimaExternoConfiguracao : IEntityTypeConfiguration<ClimaExterno>
{
    public void Configure(EntityTypeBuilder<ClimaExterno> b)
    {
        b.ToTable("ClimasExternos");
        b.HasKey(c => c.Id);
        b.Property(c => c.Estacao).HasMaxLength(10).IsRequired();
        b.HasIndex(c => new { c.Estacao, c.ObservadoEm }).IsUnique();
    }
}
