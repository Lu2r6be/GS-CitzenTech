using Microsoft.EntityFrameworkCore;
using Termometro.Application.Seguranca;
using Termometro.Domain.Entidades;
using Termometro.Infrastructure.Persistencia;

namespace Termometro.Infrastructure.Repositorios;

public sealed class AchadosRepositorio(TermometroDbContext banco) : IAchadosRepositorio
{
    public async Task AdicionarAsync(IReadOnlyCollection<AchadoSeguranca> achados, CancellationToken ct) =>
        await banco.Achados.AddRangeAsync(achados, ct);

    public Task<int> RemoverPorPrefixoDeOrigemAsync(string prefixo, CancellationToken ct) =>
        banco.Achados.Where(a => a.Origem.StartsWith(prefixo)).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<AchadoSeguranca>> ListarDesdeAsync(DateTimeOffset desde, int limite, CancellationToken ct) =>
        await banco.Achados.AsNoTracking()
            .Where(a => a.CriadoEm >= desde)
            .OrderByDescending(a => a.CriadoEm)
            .Take(limite)
            .ToListAsync(ct);
}
