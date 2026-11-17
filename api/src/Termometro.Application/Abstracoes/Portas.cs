using Termometro.Domain.Entidades;
using Termometro.Domain.Niveis;

namespace Termometro.Application.Abstracoes;

// "Portas": o que a Application precisa do mundo de fora. A Infrastructure implementa.

public interface IUnidadeDeTrabalho
{
    /// <summary>Grava tudo o que foi alterado de uma vez (ou nada).</summary>
    Task SalvarAsync(CancellationToken ct);
}

/// <summary>Leituras, casas, limiares e clima: tudo o que o semáforo precisa.</summary>
public interface IMonitoramentoRepositorio
{
    Task<Dispositivo?> ObterDispositivoAsync(string dispositivoId, CancellationToken ct);

    /// <summary>Casa já "rastreada" pelo EF: alterações nela são gravadas no próximo SalvarAsync.</summary>
    Task<Domicilio?> ObterDomicilioAsync(Guid id, CancellationToken ct);

    Task<Domicilio?> ObterDomicilioDoResidenteAsync(Guid residenteId, CancellationToken ct);

    Task<Residente?> ObterResidenteAsync(Guid residenteId, CancellationToken ct);

    Task<IReadOnlyList<CasaMonitorada>> ListarCasasDaMicroareaAsync(Guid microareaId, CancellationToken ct);

    Task AdicionarLeituraAsync(Leitura leitura, CancellationToken ct);

    /// <summary>Leituras da casa, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<Leitura>> ListarLeiturasAsync(Guid domicilioId, DateTimeOffset? de,
        DateTimeOffset? ate, int limite, CancellationToken ct);

    Task<LimiarTemperatura?> ObterLimiarAtualAsync(CancellationToken ct);
    Task<DiaRisco?> ObterDiaRiscoAsync(DateOnly data, CancellationToken ct);
    Task<ClimaExterno?> ObterClimaMaisRecenteAsync(CancellationToken ct);
}

public interface IVisitasRepositorio
{
    Task<Visita?> ObterAsync(Guid id, CancellationToken ct);
    Task AdicionarAsync(Visita visita, CancellationToken ct);
}

public interface ICadastroRepositorio
{
    Task<Usuario?> ObterUsuarioPorEmailAsync(string email, CancellationToken ct);
    Task<bool> MicroareaExisteAsync(Guid id, CancellationToken ct);
    Task<bool> ResidenteExisteAsync(Guid id, CancellationToken ct);
    Task<bool> CpfJaCadastradoAsync(Cpf cpf, CancellationToken ct);
    Task AdicionarResidenteAsync(Residente residente, CancellationToken ct);
    Task AdicionarDomicilioAsync(Domicilio domicilio, CancellationToken ct);
    Task AdicionarConsentimentoAsync(Consentimento consentimento, CancellationToken ct);
}

/// <summary>Uma casa com o morador e o último sinal dela: a linha da fila do ACS.</summary>
public sealed record CasaMonitorada(
    Domicilio Domicilio,
    Residente Residente,
    Leitura? UltimaLeitura,
    DateTimeOffset? UltimaVisitaRealizadaEm);
