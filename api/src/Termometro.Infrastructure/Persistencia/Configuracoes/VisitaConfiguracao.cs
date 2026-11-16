using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class VisitaConfiguracao : IEntityTypeConfiguration<Visita>
{
    public void Configure(EntityTypeBuilder<Visita> b)
    {
        b.ToTable("Visitas");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).ValueGeneratedNever(); // o Id nasce no app: garante a sincronização idempotente
        b.Property(v => v.Observacao).HasMaxLength(2000);
        b.Property(v => v.Resultado).HasConversion<string>().HasMaxLength(20);
        b.Property(v => v.Origem).HasConversion<string>().HasMaxLength(10);
        b.HasOne<Domicilio>().WithMany().HasForeignKey(v => v.DomicilioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(v => v.AcsId).OnDelete(DeleteBehavior.Restrict);

        // "Quando foi a última visita desta casa?" — usado para o desempate da fila.
        b.HasIndex(v => new { v.DomicilioId, v.RealizadaEm });
    }
}
