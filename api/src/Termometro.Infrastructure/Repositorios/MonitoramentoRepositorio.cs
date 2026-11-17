using Microsoft.EntityFrameworkCore;
using Termometro.Application.Abstracoes;
using Termometro.Domain.Entidades;
using Termometro.Domain.Niveis;
using Termometro.Infrastructure.Persistencia;

namespace Termometro.Infrastructure.Repositorios;

public sealed class MonitoramentoRepositorio(TermometroDbContext banco) : IMonitoramentoRepositorio
{
    public Task<Dispositivo?> ObterDispositivoAsync(string dispositivoId, CancellationToken ct) =>
        banco.Dispositivos.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dispositivoId, ct);

    // Com rastreamento de propósito: o caso de uso altera o estado do semáforo e o SalvarAsync grava.
    public Task<Domicilio?> ObterDomicilioAsync(Guid id, CancellationToken ct) =>
        banco.Domicilios.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<Domicilio?> ObterDomicilioDoResidenteAsync(Guid residenteId, CancellationToken ct) =>
        banco.Domicilios.AsNoTracking().FirstOrDefaultAsync(d => d.ResidenteId == residenteId, ct);

    public Task<Residente?> ObterResidenteAsync(Guid residenteId, CancellationToken ct) =>
        banco.Residentes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == residenteId, ct);

    public async Task<IReadOnlyList<CasaMonitorada>> ListarCasasDaMicroareaAsync(Guid microareaId, CancellationToken ct)
    {
        // Passo 1: as casas da microárea com o morador (um join, como no contrato: relações só por Id).
        var casas = await (from d in banco.Domicilios.AsNoTracking()
                           join r in banco.Residentes.AsNoTracking() on d.ResidenteId equals r.Id
                           where d.MicroareaId == microareaId
                           select new { Casa = d, Morador = r }).ToListAsync(ct);

        var idsDasCasas = casas.Select(c => c.Casa.Id).ToList();

        // Passo 2: a leitura mais recente de cada casa. O Id da leitura cresce com o tempo,
        // então "maior Id do dispositivo" = "última leitura" (barato e sem empate).
        var ultimasLeituras = await (from l in banco.Leituras.AsNoTracking()
                                     join dv in banco.Dispositivos.AsNoTracking() on l.DispositivoId equals dv.Id
                                     where idsDasCasas.Contains(dv.DomicilioId)
                                        && l.Id == banco.Leituras.Where(x => x.DispositivoId == l.DispositivoId)
                                                                 .Max(x => x.Id)
                                     select new { dv.DomicilioId, Leitura = l }).ToListAsync(ct);

        // Passo 3: a data da última visita realizada de cada casa.
        var ultimasVisitas = await banco.Visitas.AsNoTracking()
            .Where(v => idsDasCasas.Contains(v.DomicilioId) && v.RealizadaEm != null)
            .GroupBy(v => v.DomicilioId)
            .Select(g => new { DomicilioId = g.Key, Quando = g.Max(v => v.RealizadaEm) })
            .ToListAsync(ct);

        var leituraPorCasa = ultimasLeituras.GroupBy(x => x.DomicilioId)
            .ToDictionary(g => g.Key, g => g.First().Leitura);
        var visitaPorCasa = ultimasVisitas.ToDictionary(x => x.DomicilioId, x => x.Quando);

        return casas.Select(c => new CasaMonitorada(
            c.Casa,
            c.Morador,
            leituraPorCasa.GetValueOrDefault(c.Casa.Id),
            visitaPorCasa.GetValueOrDefault(c.Casa.Id))).ToList();
    }

    public async Task AdicionarLeituraAsync(Leitura leitura, CancellationToken ct) =>
        await banco.Leituras.AddAsync(leitura, ct);

    public async Task<IReadOnlyList<Leitura>> ListarLeiturasAsync(Guid domicilioId, DateTimeOffset? de,
        DateTimeOffset? ate, int limite, CancellationToken ct)
    {
        var consulta = from l in banco.Leituras.AsNoTracking()
                       join dv in banco.Dispositivos.AsNoTracking() on l.DispositivoId equals dv.Id
                       where dv.DomicilioId == domicilioId
                       select l;

        if (de is { } inicio)
        {
            consulta = consulta.Where(l => l.RecebidaEm >= inicio);
        }
        if (ate is { } fim)
        {
            consulta = consulta.Where(l => l.RecebidaEm <= fim);
        }

        return await consulta.OrderByDescending(l => l.RecebidaEm).Take(limite).ToListAsync(ct);
    }

    public Task<LimiarTemperatura?> ObterLimiarAtualAsync(CancellationToken ct) =>
        banco.Limiares.AsNoTracking().OrderByDescending(l => l.Id).FirstOrDefaultAsync(ct);

    public Task<DiaRisco?> ObterDiaRiscoAsync(DateOnly data, CancellationToken ct) =>
        banco.DiasRisco.AsNoTracking().Where(d => d.Data == data).OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync(ct);

    public Task<ClimaExterno?> ObterClimaMaisRecenteAsync(CancellationToken ct) =>
        banco.ClimasExternos.AsNoTracking().OrderByDescending(c => c.ObservadoEm).FirstOrDefaultAsync(ct);
}
