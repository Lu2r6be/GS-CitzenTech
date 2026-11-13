using Termometro.Domain.Calor;

namespace Termometro.Domain.Tests.Calor;

public class IndiceCalorTests
{
    private static double F2C(double f) => (f - 32) * 5 / 9;
    private static double C2F(double c) => c * 9 / 5 + 32;

    // Pontos da tabela oficial do NWS (em °F): temperatura, umidade, índice esperado.
    [Theory]
    [InlineData(80, 40, 80)]
    [InlineData(90, 70, 106)]
    [InlineData(96, 50, 108)]
    [InlineData(100, 40, 109)]
    public void Calcular_PontosDaTabelaDoNws_FicaDentroDeUmGrauFahrenheit(
        double tempF, double umidade, double esperadoF)
    {
        var resultadoC = IndiceCalor.Calcular(F2C(tempF), umidade);

        Assert.InRange(C2F(resultadoC), esperadoF - 1, esperadoF + 1);
    }

    [Fact]
    public void Calcular_TemperaturaAmena_FicaPertoDaTemperaturaDoAr()
    {
        var indice = IndiceCalor.Calcular(20, 60);

        Assert.InRange(indice, 19, 21);
    }

    [Fact]
    public void Calcular_MaisUmidadeNoCalor_DaIndiceMaior()
    {
        var seco = IndiceCalor.Calcular(34, 30);
        var umido = IndiceCalor.Calcular(34, 80);

        Assert.True(umido > seco);
    }

    [Fact]
    public void Calcular_MuitoUmidoEEntre27E30Graus_AplicaAjusteDeUmidade()
    {
        // 28 °C com 95% de umidade: o ajuste "ar muito úmido" empurra o índice para cima.
        var comAjuste = IndiceCalor.Calcular(28, 95);
        var semAjuste = IndiceCalor.Calcular(28, 85);

        Assert.True(comAjuste > semAjuste);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    public void Calcular_UmidadeInvalida_Lanca(double umidade)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IndiceCalor.Calcular(30, umidade));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Calcular_TemperaturaInvalida_Lanca(double temperatura)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IndiceCalor.Calcular(temperatura, 50));
    }
}
