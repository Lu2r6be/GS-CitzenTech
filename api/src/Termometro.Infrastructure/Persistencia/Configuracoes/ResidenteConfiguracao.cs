using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Termometro.Domain.Entidades;

namespace Termometro.Infrastructure.Persistencia.Configuracoes;

internal sealed class ResidenteConfiguracao : IEntityTypeConfiguration<Residente>
{
    public void Configure(EntityTypeBuilder<Residente> b)
    {
        b.ToTable("Residentes");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Nome).HasMaxLength(200).IsRequired();
        // No banco vão só os 11 dígitos; no código, o CPF é um objeto que se imprime mascarado.
        b.Property(r => r.Cpf)
            .HasConversion(cpf => cpf.Numeros, texto => Cpf.Criar(texto))
            .HasMaxLength(11).IsRequired();
        b.HasIndex(r => r.Cpf).IsUnique();
        b.Property(r => r.CondicoesSaude).HasMaxLength(1000);
        b.Property(r => r.TelefoneContato).HasMaxLength(30);
    }
}
