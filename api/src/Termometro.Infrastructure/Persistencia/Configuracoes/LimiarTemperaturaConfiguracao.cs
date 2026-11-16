using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Niveis;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class LimiarTemperaturaConfiguracao : IEntityTypeConfiguration<LimiarTemperatura>
{
    public void Configure(EntityTypeBuilder<LimiarTemperatura> b)
    {
        b.ToTable("Limiares");
        b.HasKey(l => l.Id);
        // Propriedades só-leitura (validadas no construtor) não são mapeadas sozinhas: mapeamos à mão.
        b.Property(l => l.AzulAbaixoDe);
        b.Property(l => l.VerdeAte);
        b.Property(l => l.AmareloAte);
        b.Property(l => l.Versao).HasMaxLength(50).IsRequired();
        b.Property(l => l.Origem).HasConversion<string>().HasMaxLength(10);
        b.HasIndex(l => l.Versao).IsUnique();
    }
}
