using Termometro.Domain.Seguranca;

namespace Termometro.Domain.Entidades;

/// <summary>
/// Visita do ACS a uma casa. O Id é gerado no APP (funciona offline) e a sincronização é idempotente:
/// enviar a mesma visita duas vezes não duplica.
/// </summary>
public sealed class Visita
{
    public Guid Id { get; private set; }
    public Guid DomicilioId { get; private set; }
    public Guid AcsId { get; private set; }
    public DateTimeOffset PlanejadaEm { get; private set; }
    public DateTimeOffset? RealizadaEm { get; private set; }

    [Pii(CategoriaPii.Saude)]
    public string? Observacao { get; private set; }

    public ResultadoVisita Resultado { get; private set; }
    public OrigemVisita Origem { get; private set; }
    public DateTimeOffset? SincronizadaEm { get; private set; }

    private Visita() { }

    public Visita(Guid id, Guid domicilioId, Guid acsId, DateTimeOffset planejadaEm,
        OrigemVisita origem = OrigemVisita.Online)
    {
        if (id == Guid.Empty || domicilioId == Guid.Empty || acsId == Guid.Empty)
        {
            throw new ArgumentException("Id, domicílio e ACS são obrigatórios.");
        }

        Id = id;
        DomicilioId = domicilioId;
        AcsId = acsId;
        PlanejadaEm = planejadaEm;
        Origem = origem;
        Resultado = ResultadoVisita.NaoRealizada;
    }

    public void Realizar(DateTimeOffset em, ResultadoVisita resultado, string? observacao = null)
    {
        if (resultado == ResultadoVisita.NaoRealizada)
        {
            throw new ArgumentException("Uma visita realizada precisa de um resultado.", nameof(resultado));
        }
        if (RealizadaEm is not null)
        {
            throw new InvalidOperationException("A visita já foi realizada.");
        }

        RealizadaEm = em;
        Resultado = resultado;
        Observacao = observacao;
    }

    public void MarcarSincronizada(DateTimeOffset em) => SincronizadaEm = em;
}
