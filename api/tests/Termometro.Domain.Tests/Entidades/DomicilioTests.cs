using Termometro.Domain.Entidades;
using Termometro.Domain.Niveis;
using static Termometro.Domain.Niveis.NivelTermico;

namespace Termometro.Domain.Tests.Entidades;

public class DomicilioTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private static Domicilio Criar() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Rua das Flores, 10", "01000-000", TipoCobertura.Fibrocimento, 3);

    [Fact]
    public void RegistrarLeitura_PrimeiraLeitura_ViraONivelConfirmado()
    {
        var casa = Criar();

        var nivel = casa.RegistrarLeitura(Amarelo, T0);

        Assert.Equal(Amarelo, nivel);
        Assert.Equal(T0, casa.UltimaLeituraEm);
    }

    [Fact]
    public void RegistrarLeitura_PiorarExige3LeiturasSeguidas()
    {
        var casa = Criar();
        casa.RegistrarLeitura(Verde, T0);

        Assert.Equal(Verde, casa.RegistrarLeitura(Vermelho, T0.AddMinutes(5)));
        Assert.Equal(Verde, casa.RegistrarLeitura(Vermelho, T0.AddMinutes(10)));
        Assert.Equal(Vermelho, casa.RegistrarLeitura(Vermelho, T0.AddMinutes(15)));
        Assert.Equal(Vermelho, casa.NivelConfirmado);
    }

    [Fact]
    public void RegistrarLeitura_GuardaOEstadoDaConfirmacaoNaPropriaCasa()
    {
        var casa = Criar();
        casa.RegistrarLeitura(Verde, T0);
        casa.RegistrarLeitura(Amarelo, T0.AddMinutes(5));

        Assert.Equal(Verde, casa.NivelConfirmado);
        Assert.Equal(Amarelo, casa.NivelCandidato);
        Assert.Equal(1, casa.LeiturasDoCandidato);
    }

    [Fact]
    public void RegistrarLeitura_LeituraAtrasada_ENaoMudaNada()
    {
        var casa = Criar();
        casa.RegistrarLeitura(Verde, T0);

        var nivel = casa.RegistrarLeitura(Vermelho, T0.AddMinutes(-10));

        Assert.Equal(Verde, nivel);
        Assert.Equal(T0, casa.UltimaLeituraEm);
        Assert.Null(casa.NivelCandidato);
    }

    [Fact]
    public void NivelExibido_CasaQueNuncaEnviou_ECinza()
    {
        Assert.Equal(Cinza, Criar().NivelExibido(T0));
    }

    [Fact]
    public void NivelExibido_ComLeituraRecente_MostraONivelConfirmado()
    {
        var casa = Criar();
        casa.RegistrarLeitura(Vermelho, T0);

        Assert.Equal(Vermelho, casa.NivelExibido(T0.AddMinutes(29)));
    }

    [Fact]
    public void NivelExibido_Apos30MinutosSemLeitura_ViraCinza()
    {
        var casa = Criar();
        casa.RegistrarLeitura(Verde, T0);

        Assert.Equal(Cinza, casa.NivelExibido(T0.AddMinutes(30)));
    }

    [Fact]
    public void Construtor_SemResidenteOuMicroarea_Lanca()
    {
        Assert.Throws<ArgumentException>(
            () => new Domicilio(Guid.Empty, Guid.NewGuid(), "Rua A", "01000-000", TipoCobertura.Laje));
        Assert.Throws<ArgumentException>(
            () => new Domicilio(Guid.NewGuid(), Guid.Empty, "Rua A", "01000-000", TipoCobertura.Laje));
    }
}
