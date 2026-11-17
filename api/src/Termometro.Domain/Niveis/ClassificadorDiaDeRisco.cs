namespace Termometro.Domain.Niveis;

/// <summary>
/// Dia de risco a partir do clima EXTERNO, com os cortes que o ML achou (RELATORIO_ML.md):
/// calor = máxima ≥ 36 °C; frio = mínima ≤ 13 °C. Usado quando a tabela DiaRisco não tem o dia.
/// </summary>
public static class ClassificadorDiaDeRisco
{
    public const double CalorMaximaAPartirDeC = 36;
    public const double FrioMinimaAteC = 13;

    public static TipoDiaRisco Classificar(double tempMaxC, double tempMinC) =>
        tempMaxC >= CalorMaximaAPartirDeC ? TipoDiaRisco.Calor
        : tempMinC <= FrioMinimaAteC ? TipoDiaRisco.Frio
        : TipoDiaRisco.Nenhum;
}
