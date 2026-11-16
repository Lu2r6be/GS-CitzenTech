using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Termometro.Infrastructure.Persistencia;

/// <summary>
/// Guarda DateTimeOffset como data/hora UTC. O PostgreSQL (timestamptz) só aceita UTC, e assim
/// o SQLite dos testes também consegue ordenar por data.
/// </summary>
public sealed class ConversorDataHoraUtc() : ValueConverter<DateTimeOffset, DateTime>(
    valor => valor.UtcDateTime,
    banco => new DateTimeOffset(DateTime.SpecifyKind(banco, DateTimeKind.Utc)));
