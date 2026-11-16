using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class ConsentimentoConfiguracao : IEntityTypeConfiguration<Consentimento>
{
    public void Configure(EntityTypeBuilder<Consentimento> b)
    {
        b.ToTable("Consentimentos");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Finalidade).HasMaxLength(300).IsRequired();
        b.Ignore(c => c.EstaAtivo); // calculado a partir de RevogadoEm
        b.HasOne<Residente>().WithMany().HasForeignKey(c => c.ResidenteId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => c.ResidenteId);
    }
}
