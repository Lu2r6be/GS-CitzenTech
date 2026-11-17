using Termometro.Application.Abstracoes;

namespace Termometro.Infrastructure.Persistencia;

/// <summary>"Salvar tudo de uma vez": o DbContext já faz isso numa transação só.</summary>
public sealed class UnidadeDeTrabalho(TermometroDbContext banco) : IUnidadeDeTrabalho
{
    public Task SalvarAsync(CancellationToken ct) => banco.SaveChangesAsync(ct);
}
