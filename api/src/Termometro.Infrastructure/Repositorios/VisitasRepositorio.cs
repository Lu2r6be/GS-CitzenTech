using Microsoft.EntityFrameworkCore;
using Termometro.Application.Abstracoes;
using Termometro.Domain.Entidades;
using Termometro.Infrastructure.Persistencia;

namespace Termometro.Infrastructure.Repositorios;

public sealed class VisitasRepositorio(TermometroDbContext banco) : IVisitasRepositorio
{
    public Task<Visita?> ObterAsync(Guid id, CancellationToken ct) =>
        banco.Visitas.FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task AdicionarAsync(Visita visita, CancellationToken ct) =>
        await banco.Visitas.AddAsync(visita, ct);
}
