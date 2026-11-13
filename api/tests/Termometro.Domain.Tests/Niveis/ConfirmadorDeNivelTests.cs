using Termometro.Domain.Niveis;
using static Termometro.Domain.Niveis.NivelTermico;

namespace Termometro.Domain.Tests.Niveis;

public class ConfirmadorDeNivelTests
{
    private static ConfirmadorDeNivel Com(NivelTermico inicial)
    {
        var c = new ConfirmadorDeNivel();
        c.Registrar(inicial);
        return c;
    }

    [Fact]
    public void Registrar_PrimeiraLeitura_AdotaONivelNaHora()
    {
        var c = new ConfirmadorDeNivel();

        Assert.Equal(Vermelho, c.Registrar(Vermelho));
    }

    [Fact]
    public void Registrar_PiorarExige3LeiturasSeguidas()
    {
        var c = Com(Verde);

        Assert.Equal(Verde, c.Registrar(Amarelo));
        Assert.Equal(Verde, c.Registrar(Amarelo));
        Assert.Equal(Amarelo, c.Registrar(Amarelo));
    }

    [Fact]
    public void Registrar_MelhorarExige6LeiturasSeguidas()
    {
        var c = Com(Vermelho);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(Vermelho, c.Registrar(Verde));
        }
        Assert.Equal(Verde, c.Registrar(Verde));
    }

    [Fact]
    public void Registrar_LeituraDiferenteNoMeio_ZeraAContagem()
    {
        var c = Com(Verde);

        c.Registrar(Amarelo);
        c.Registrar(Amarelo);
        c.Registrar(Verde);            // voltou ao normal: a contagem recomeça
        c.Registrar(Amarelo);
        c.Registrar(Amarelo);

        Assert.Equal(Verde, c.Atual);
        Assert.Equal(Amarelo, c.Registrar(Amarelo));
    }

    [Fact]
    public void Registrar_CandidatosAlternados_NuncaConfirma()
    {
        var c = Com(Verde);

        for (var i = 0; i < 10; i++)
        {
            c.Registrar(i % 2 == 0 ? Amarelo : Vermelho);
        }

        Assert.Equal(Verde, c.Atual);
    }

    [Fact]
    public void Registrar_FrioContaComoPiorar()
    {
        var c = Com(Verde);

        c.Registrar(Azul);
        c.Registrar(Azul);

        Assert.Equal(Azul, c.Registrar(Azul));
    }

    [Fact]
    public void Registrar_DeVermelhoParaAmarelo_ContaComoMelhorar()
    {
        var c = Com(Vermelho);

        for (var i = 0; i < 5; i++)
        {
            c.Registrar(Amarelo);
        }

        Assert.Equal(Vermelho, c.Atual);
        Assert.Equal(Amarelo, c.Registrar(Amarelo));
    }

    [Fact]
    public void Registrar_Cinza_Lanca()
    {
        var c = Com(Verde);

        Assert.Throws<ArgumentException>(() => c.Registrar(Cinza));
    }

    [Fact]
    public void Restaurar_EstadoSalvo_ContinuaDeOndeParou()
    {
        var c = Com(Verde);
        c.Registrar(Amarelo);
        c.Registrar(Amarelo);

        // simula guardar no banco e carregar na próxima leitura
        var restaurado = new ConfirmadorDeNivel(c.Atual, c.Candidato, c.LeiturasDoCandidato);

        Assert.Equal(Amarelo, restaurado.Registrar(Amarelo));
    }

    [Fact]
    public void Restaurar_EstadoInconsistente_Lanca()
    {
        Assert.Throws<ArgumentException>(() => new ConfirmadorDeNivel(Verde, Amarelo, 0));
        Assert.Throws<ArgumentException>(() => new ConfirmadorDeNivel(Verde, null, 2));
    }
}
