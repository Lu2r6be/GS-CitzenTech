using Termometro.Domain.Niveis;

namespace Termometro.Domain.Tests.Niveis;

public class LimiarTemperaturaTests
{
    [Fact]
    public void Padrao_TemAsFaixasIniciais()
    {
        var limiares = LimiarTemperatura.Padrao;

        Assert.Equal(18, limiares.AzulAbaixoDe);
        Assert.Equal(32, limiares.VerdeAte);
        Assert.Equal(39, limiares.AmareloAte);
        Assert.Equal(OrigemLimiar.Regra, limiares.Origem);
    }

    [Theory]
    [InlineData(32, 32, 39)]   // azul = verde
    [InlineData(18, 40, 39)]   // verde > amarelo
    [InlineData(40, 32, 39)]   // azul > verde
    public void Construtor_FaixasForaDeOrdem_Lanca(double azul, double verde, double amarelo)
    {
        Assert.Throws<ArgumentException>(
            () => new LimiarTemperatura(azul, verde, amarelo, "v1", OrigemLimiar.Regra));
    }

    [Fact]
    public void Construtor_SemVersao_Lanca()
    {
        Assert.Throws<ArgumentException>(
            () => new LimiarTemperatura(18, 32, 39, " ", OrigemLimiar.Regra));
    }
}
