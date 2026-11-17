namespace Termometro.Domain.Niveis;

public enum SentidoTendencia
{
    Estavel = 0,
    Subindo = 1,
    Descendo = 2,
}

/// <summary>
/// Seta de tendência do app (subindo / estável / descendo) olhando a última hora.
/// É só para leitura humana: a decisão (fila, alerta) usa os níveis discretos.
/// </summary>
public static class Tendencia
{
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(60);

    /// <summary>Variação mínima (°C) para considerar que a temperatura mudou de verdade.</summary>
    public const double VariacaoMinimaC = 0.5;

    public static SentidoTendencia Calcular(IEnumerable<(DateTimeOffset Quando, double TemperaturaC)> leituras,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(leituras);

        var naJanela = leituras
            .Where(l => l.Quando <= agora && agora - l.Quando <= Janela)
            .OrderBy(l => l.Quando)
            .ToList();

        if (naJanela.Count < 2)
        {
            return SentidoTendencia.Estavel;
        }

        var variacao = naJanela[^1].TemperaturaC - naJanela[0].TemperaturaC;
        return variacao >= VariacaoMinimaC ? SentidoTendencia.Subindo
             : variacao <= -VariacaoMinimaC ? SentidoTendencia.Descendo
             : SentidoTendencia.Estavel;
    }
}
