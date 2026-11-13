namespace Termometro.Domain.Niveis;

/// <summary>
/// Cor do semáforo de uma casa. Cinza significa "sem sinal do sensor":
/// sensor mudo nunca pode parecer "seguro".
/// </summary>
public enum NivelTermico
{
    Cinza = 0,
    Azul = 1,
    Verde = 2,
    Amarelo = 3,
    Vermelho = 4,
}

public static class NivelTermicoExtensoes
{
    /// <summary>
    /// Gravidade usada pela confirmação de leituras (histerese): quanto maior, pior.
    /// Azul e Amarelo têm a mesma gravidade (são riscos de lados opostos, mas moderados).
    /// Cinza não tem gravidade: ele não vem de uma leitura do sensor.
    /// </summary>
    public static int Gravidade(this NivelTermico nivel) => nivel switch
    {
        NivelTermico.Verde => 0,
        NivelTermico.Azul => 1,
        NivelTermico.Amarelo => 1,
        NivelTermico.Vermelho => 2,
        _ => throw new ArgumentException($"O nível {nivel} não tem gravidade.", nameof(nivel)),
    };
}
