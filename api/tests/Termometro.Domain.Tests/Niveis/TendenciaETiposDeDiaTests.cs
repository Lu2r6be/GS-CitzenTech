using Termometro.Domain.Niveis;

namespace Termometro.Domain.Tests.Niveis;

public class TendenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private static (DateTimeOffset, double) L(int minutosAtras, double temp) => (Agora.AddMinutes(-minutosAtras), temp);

    [Fact]
    public void Tendencia_TemperaturaSubiuMeioGrau_Subindo() =>
        Assert.Equal(SentidoTendencia.Subindo, Tendencia.Calcular([L(50, 30.0), L(10, 30.5)], Agora));

    [Fact]
    public void Tendencia_TemperaturaCaiu_Descendo() =>
        Assert.Equal(SentidoTendencia.Descendo, Tendencia.Calcular([L(50, 33.0), L(25, 31.0), L(1, 30.0)], Agora));

    [Fact]
    public void Tendencia_VariacaoPequena_Estavel() =>
        Assert.Equal(SentidoTendencia.Estavel, Tendencia.Calcular([L(40, 30.0), L(1, 30.4)], Agora));

    [Fact]
    public void Tendencia_UmaSoLeitura_Estavel() =>
        Assert.Equal(SentidoTendencia.Estavel, Tendencia.Calcular([L(5, 30.0)], Agora));

    [Fact]
    public void Tendencia_IgnoraLeiturasComMaisDeUmaHora()
    {
        // A leitura de 90 min atrás (20 °C) não pode contar: dentro da janela só há 30 → 30,2.
        var sentido = Tendencia.Calcular([L(90, 20.0), L(30, 30.0), L(1, 30.2)], Agora);

        Assert.Equal(SentidoTendencia.Estavel, sentido);
    }

    [Fact]
    public void Tendencia_NaoDependeDaOrdemDeEntrada() =>
        Assert.Equal(SentidoTendencia.Subindo, Tendencia.Calcular([L(1, 32.0), L(50, 30.0)], Agora));
}

public class ClassificadorDiaDeRiscoTests
{
    [Theory]
    [InlineData(36.0, 22.0, TipoDiaRisco.Calor)]
    [InlineData(35.9, 22.0, TipoDiaRisco.Nenhum)]
    [InlineData(25.0, 13.0, TipoDiaRisco.Frio)]
    [InlineData(25.0, 13.1, TipoDiaRisco.Nenhum)]
    public void DiaDeRisco_ApareceNosCortesDoML(double max, double min, TipoDiaRisco esperado) =>
        Assert.Equal(esperado, ClassificadorDiaDeRisco.Classificar(max, min));
}
