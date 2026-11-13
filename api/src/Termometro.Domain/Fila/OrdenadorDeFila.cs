using Termometro.Domain.Niveis;

namespace Termometro.Domain.Fila;

public sealed record ItemFila(
    Guid DomicilioId,
    NivelTermico Nivel,
    Vulnerabilidade Vulnerabilidade,
    TimeSpan? TempoDesdeUltimaVisita);

public interface IOrdenadorDeFila
{
    IReadOnlyList<ItemFila> Ordenar(IEnumerable<ItemFila> itens, TipoDiaRisco diaDeRisco);
}

/// <summary>
/// Fila de visitas do agente comunitário de saúde (ACS).
/// Ordem: sem sinal &gt; vermelho (e azul em dia de frio) &gt; azul &gt; amarelo &gt; verde.
/// Empate: mais vulnerável primeiro; depois, quem está há mais tempo sem visita
/// (nunca visitado conta como o mais antigo).
/// </summary>
public sealed class OrdenadorDeFila : IOrdenadorDeFila
{
    public IReadOnlyList<ItemFila> Ordenar(IEnumerable<ItemFila> itens, TipoDiaRisco diaDeRisco)
    {
        ArgumentNullException.ThrowIfNull(itens);

        return itens
            .OrderBy(i => Faixa(i.Nivel, diaDeRisco))
            .ThenByDescending(i => i.Vulnerabilidade.Pontos)
            .ThenByDescending(i => i.TempoDesdeUltimaVisita ?? TimeSpan.MaxValue)
            .ThenBy(i => i.DomicilioId) // desempate final: a mesma entrada sempre dá a mesma fila
            .ToList();
    }

    private static int Faixa(NivelTermico nivel, TipoDiaRisco dia) => nivel switch
    {
        NivelTermico.Cinza => 0,
        NivelTermico.Vermelho => 1,
        NivelTermico.Azul when dia == TipoDiaRisco.Frio => 1,
        NivelTermico.Azul => 2,
        NivelTermico.Amarelo => 3,
        NivelTermico.Verde => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(nivel)),
    };
}
