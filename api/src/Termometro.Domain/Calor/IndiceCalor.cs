namespace Termometro.Domain.Calor;

/// <summary>
/// Índice de calor ("sensação térmica de calor"): combina temperatura e umidade.
/// Mesma lógica do NWS (serviço meteorológico dos EUA): fórmula simples para temperaturas
/// amenas e a regressão de Rothfusz a partir de ~27 °C (80 °F), com os dois ajustes oficiais.
/// Função pura: mesma entrada, sempre a mesma saída, sem relógio, banco ou internet.
/// </summary>
public static class IndiceCalor
{
    private const double LimiteRothfuszF = 80.0;

    public static double Calcular(double temperaturaC, double umidadePercentual)
    {
        if (!double.IsFinite(temperaturaC))
        {
            throw new ArgumentOutOfRangeException(nameof(temperaturaC), "Temperatura inválida.");
        }
        if (!double.IsFinite(umidadePercentual) || umidadePercentual is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(umidadePercentual), "A umidade relativa precisa estar entre 0 e 100.");
        }

        var t = CelsiusParaFahrenheit(temperaturaC);
        var u = umidadePercentual;

        var simples = 0.5 * (t + 61.0 + ((t - 68.0) * 1.2) + (u * 0.094));
        if ((simples + t) / 2.0 < LimiteRothfuszF)
        {
            return FahrenheitParaCelsius(simples);
        }

        return FahrenheitParaCelsius(Rothfusz(t, u));
    }

    private static double Rothfusz(double t, double u)
    {
        var hi = -42.379
                 + 2.04901523 * t
                 + 10.14333127 * u
                 - 0.22475541 * t * u
                 - 0.00683783 * t * t
                 - 0.05481717 * u * u
                 + 0.00122874 * t * t * u
                 + 0.00085282 * t * u * u
                 - 0.00000199 * t * t * u * u;

        // Ajuste para ar muito seco.
        if (u < 13 && t is >= 80 and <= 112)
        {
            hi -= ((13 - u) / 4.0) * Math.Sqrt((17 - Math.Abs(t - 95.0)) / 17.0);
        }
        // Ajuste para ar muito úmido.
        else if (u > 85 && t is >= 80 and <= 87)
        {
            hi += ((u - 85) / 10.0) * ((87 - t) / 5.0);
        }

        return hi;
    }

    private static double CelsiusParaFahrenheit(double c) => c * 9.0 / 5.0 + 32.0;

    private static double FahrenheitParaCelsius(double f) => (f - 32.0) * 5.0 / 9.0;
}
