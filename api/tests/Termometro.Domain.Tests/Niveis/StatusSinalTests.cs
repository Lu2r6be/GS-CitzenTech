using Termometro.Domain.Niveis;

namespace Termometro.Domain.Tests.Niveis;

public class StatusSinalTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EstaSemSinal_Com29MinutosDeSilencio_AindaTemSinal()
    {
        Assert.False(StatusSinal.EstaSemSinal(Agora.AddMinutes(-29), Agora));
    }

    [Fact]
    public void EstaSemSinal_Com30MinutosDeSilencio_FicaSemSinal()
    {
        Assert.True(StatusSinal.EstaSemSinal(Agora.AddMinutes(-30), Agora));
    }

    [Fact]
    public void EstaSemSinal_CasaQueNuncaEnviou_FicaSemSinal()
    {
        Assert.True(StatusSinal.EstaSemSinal(null, Agora));
    }

    [Fact]
    public void EstaSemSinal_ComToleranciaPropria_UsaATolerancia()
    {
        Assert.True(StatusSinal.EstaSemSinal(Agora.AddMinutes(-6), Agora, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void EstaSemSinal_LeituraComRelogioAdiantado_ContaComoSinal()
    {
        // relógio do dispositivo um pouco à frente: não pode virar "sem sinal" por isso
        Assert.False(StatusSinal.EstaSemSinal(Agora.AddMinutes(2), Agora));
    }
}
