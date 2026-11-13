namespace Termometro.Domain.Niveis;

/// <summary>
/// Tipo do dia segundo o ML (clima externo): calor ou frio perigoso para idosos.
/// Os limiares (máxima ≥ 36 °C, mínima ≤ 13 °C) vêm de ml/resultados/limiares_ml.json.
/// </summary>
public enum TipoDiaRisco
{
    Nenhum = 0,
    Calor = 1,
    Frio = 2,
}
