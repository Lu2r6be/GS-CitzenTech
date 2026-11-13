using Termometro.Domain.Calor;
using Termometro.Domain.Niveis;

namespace Termometro.Domain.Tests.Calor;

public class ClassificadorNivelTests
{
    private readonly IClassificadorNivel _classificador = new ClassificadorNivel();
    private readonly LimiarTemperatura _limiares = LimiarTemperatura.Padrao;

    [Theory]
    [InlineData(15, 50, NivelTermico.Azul)]
    [InlineData(17.9, 50, NivelTermico.Azul)]
    [InlineData(18, 50, NivelTermico.Verde)]   // 18 °C já não é frio perigoso
    [InlineData(20, 60, NivelTermico.Verde)]
    [InlineData(28, 50, NivelTermico.Verde)]
    [InlineData(32, 45, NivelTermico.Amarelo)]  // índice de calor ≈ 33 °C
    [InlineData(34, 40, NivelTermico.Amarelo)]  // índice ≈ 35 °C
    [InlineData(33, 60, NivelTermico.Vermelho)] // índice ≈ 39,5 °C: umidade pesa
    [InlineData(35, 70, NivelTermico.Vermelho)]
    public void Classificar_SemDiaDeRisco_RetornaONivelDaFaixa(
        double temperatura, double umidade, NivelTermico esperado)
    {
        var nivel = _classificador.Classificar(temperatura, umidade, _limiares);

        Assert.Equal(esperado, nivel);
    }

    [Theory]
    [InlineData(25, 50, NivelTermico.Amarelo)]   // verde sobe para amarelo
    [InlineData(32, 45, NivelTermico.Vermelho)]  // amarelo sobe para vermelho
    [InlineData(35, 70, NivelTermico.Vermelho)]  // vermelho continua vermelho
    [InlineData(15, 50, NivelTermico.Azul)]      // frio não é afetado por dia de calor
    public void Classificar_EmDiaDeCalor_SobeUmDegrauNoCalor(
        double temperatura, double umidade, NivelTermico esperado)
    {
        var nivel = _classificador.Classificar(temperatura, umidade, _limiares, TipoDiaRisco.Calor);

        Assert.Equal(esperado, nivel);
    }

    [Theory]
    [InlineData(25, 50, NivelTermico.Verde)]
    [InlineData(15, 50, NivelTermico.Azul)]
    [InlineData(32, 45, NivelTermico.Amarelo)]
    public void Classificar_EmDiaDeFrio_NaoMudaACor(
        double temperatura, double umidade, NivelTermico esperado)
    {
        var nivel = _classificador.Classificar(temperatura, umidade, _limiares, TipoDiaRisco.Frio);

        Assert.Equal(esperado, nivel);
    }

    [Fact]
    public void Classificar_ComLimiaresDoMl_UsaOsLimiaresRecebidos()
    {
        var exigente = new LimiarTemperatura(20, 25, 30, "teste", OrigemLimiar.ML);

        var nivel = _classificador.Classificar(26, 50, exigente);

        Assert.Equal(NivelTermico.Amarelo, nivel);
    }

    [Fact]
    public void Classificar_SemLimiares_Lanca()
    {
        Assert.Throws<ArgumentNullException>(() => _classificador.Classificar(25, 50, null!));
    }
}
