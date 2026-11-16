using Termometro.Domain.Entidades;

namespace Termometro.Domain.Tests.Entidades;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    public void Criar_CpfValido_Aceita(string texto)
    {
        var cpf = Cpf.Criar(texto);

        Assert.Equal(11, cpf.Numeros.Length);
    }

    [Theory]
    [InlineData("529.982.247-24")]   // dígito verificador errado
    [InlineData("111.111.111-11")]   // dígitos repetidos
    [InlineData("123")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc.def.ghi-jk")]
    public void Criar_CpfInvalido_Lanca(string texto)
    {
        Assert.Throws<ArgumentException>(() => Cpf.Criar(texto));
    }

    [Fact]
    public void TentarCriar_Nulo_DevolveFalso()
    {
        Assert.False(Cpf.TentarCriar(null, out var cpf));
        Assert.Null(cpf);
    }

    [Fact]
    public void Mascarado_EscondeOsPrimeiros3EOs2UltimosDigitos()
    {
        var cpf = Cpf.Criar("529.982.247-25");

        Assert.Equal("***.982.247-**", cpf.Mascarado);
    }

    [Fact]
    public void ToString_NuncaDevolveOCpfCompleto()
    {
        var cpf = Cpf.Criar("52998224725");

        Assert.DoesNotContain("52998224725", cpf.ToString());
        Assert.DoesNotContain("529", cpf.ToString());
        Assert.DoesNotContain("529", $"{cpf}"); // também na interpolação usada em logs
    }

    [Fact]
    public void Igualdade_DoisCpfsComOsMesmosNumeros_SaoIguais()
    {
        Assert.Equal(Cpf.Criar("529.982.247-25"), Cpf.Criar("52998224725"));
    }
}
