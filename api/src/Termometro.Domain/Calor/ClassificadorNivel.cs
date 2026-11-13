using Termometro.Domain.Niveis;

namespace Termometro.Domain.Calor;

public interface IClassificadorNivel
{
    /// <summary>Transforma uma leitura do sensor (dado bruto) em um nível do semáforo.</summary>
    NivelTermico Classificar(double temperaturaC, double umidadePercentual,
        LimiarTemperatura limiares, TipoDiaRisco diaDeRisco = TipoDiaRisco.Nenhum);
}

public sealed class ClassificadorNivel : IClassificadorNivel
{
    public NivelTermico Classificar(double temperaturaC, double umidadePercentual,
        LimiarTemperatura limiares, TipoDiaRisco diaDeRisco = TipoDiaRisco.Nenhum)
    {
        ArgumentNullException.ThrowIfNull(limiares);

        // Frio usa a temperatura simples: o índice de calor não se aplica ao frio.
        if (temperaturaC < limiares.AzulAbaixoDe)
        {
            return NivelTermico.Azul;
        }

        var indice = IndiceCalor.Calcular(temperaturaC, umidadePercentual);
        var nivel = indice < limiares.VerdeAte ? NivelTermico.Verde
                  : indice < limiares.AmareloAte ? NivelTermico.Amarelo
                  : NivelTermico.Vermelho;

        // Em dia de calor perigoso lá fora, o nível de calor sobe um degrau.
        // Em dia de frio a COR não muda; o que muda é a prioridade na fila (OrdenadorDeFila).
        return diaDeRisco == TipoDiaRisco.Calor ? SubirUmDegrau(nivel) : nivel;
    }

    private static NivelTermico SubirUmDegrau(NivelTermico nivel) => nivel switch
    {
        NivelTermico.Verde => NivelTermico.Amarelo,
        NivelTermico.Amarelo => NivelTermico.Vermelho,
        _ => nivel,
    };
}
