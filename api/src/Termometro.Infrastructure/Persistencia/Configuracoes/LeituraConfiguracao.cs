using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class LeituraConfiguracao : IEntityTypeConfiguration<Leitura>
{
    public void Configure(EntityTypeBuilder<Leitura> b)
    {
        b.ToTable("Leituras");
        b.HasKey(l => l.Id);
        b.Property(l => l.DispositivoId).HasMaxLength(50).IsRequired();
        b.Property(l => l.Nivel).HasConversion<string>().HasMaxLength(10);
        b.HasOne<Dispositivo>().WithMany().HasForeignKey(l => l.DispositivoId).OnDelete(DeleteBehavior.Restrict);

        // Tabela que mais cresce; a consulta típica é "últimas leituras deste sensor".
        b.HasIndex(l => new { l.DispositivoId, l.RecebidaEm });
    }
}
