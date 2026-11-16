using Termometro.Domain.Entidades;
using Termometro.Domain.Niveis;

namespace Termometro.Domain.Tests.Entidades;

public class UsuarioTests
{
    [Fact]
    public void CriarAcs_GuardaEmailEmMinusculasEAMicroarea()
    {
        var micro = Guid.NewGuid();

        var u = Usuario.CriarAcs("Ana", "  ANA@Exemplo.COM ", "hash", micro);

        Assert.Equal("ana@exemplo.com", u.Email);
        Assert.Equal(PapelUsuario.Acs, u.Papel);
        Assert.Equal(micro, u.MicroareaId);
        Assert.Null(u.ResidenteId);
    }

    [Fact]
    public void CriarFamiliar_GuardaOResidente()
    {
        var residente = Guid.NewGuid();

        var u = Usuario.CriarFamiliar("Luiz", "luiz@exemplo.com", "hash", residente);

        Assert.Equal(PapelUsuario.Familiar, u.Papel);
        Assert.Equal(residente, u.ResidenteId);
    }

    [Fact]
    public void CriarAdmin_NaoTemMicroareaNemResidente()
    {
        var u = Usuario.CriarAdmin("Admin", "admin@exemplo.com", "hash");

        Assert.Null(u.MicroareaId);
        Assert.Null(u.ResidenteId);
    }

    [Fact]
    public void CriarAcs_SemMicroarea_Lanca()
    {
        Assert.Throws<ArgumentException>(() => Usuario.CriarAcs("Ana", "ana@exemplo.com", "hash", Guid.Empty));
    }

    [Fact]
    public void CriarAdmin_EmailSemArroba_Lanca()
    {
        Assert.Throws<ArgumentException>(() => Usuario.CriarAdmin("Admin", "adminexemplo.com", "hash"));
    }
}

public class ConsentimentoTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Novo_EstaAtivo()
    {
        Assert.True(new Consentimento(Guid.NewGuid(), "Monitorar temperatura da casa", T0).EstaAtivo);
    }

    [Fact]
    public void Revogar_DesativaOConsentimento()
    {
        var c = new Consentimento(Guid.NewGuid(), "Monitorar temperatura da casa", T0);

        c.Revogar(T0.AddDays(1));

        Assert.False(c.EstaAtivo);
        Assert.Equal(T0.AddDays(1), c.RevogadoEm);
    }

    [Fact]
    public void Revogar_DuasVezes_Lanca()
    {
        var c = new Consentimento(Guid.NewGuid(), "Monitorar temperatura da casa", T0);
        c.Revogar(T0.AddDays(1));

        Assert.Throws<InvalidOperationException>(() => c.Revogar(T0.AddDays(2)));
    }

    [Fact]
    public void Revogar_AntesDeConceder_Lanca()
    {
        var c = new Consentimento(Guid.NewGuid(), "Monitorar temperatura da casa", T0);

        Assert.Throws<ArgumentException>(() => c.Revogar(T0.AddDays(-1)));
    }
}

public class VisitaTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private static Visita Nova() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), T0, OrigemVisita.Offline);

    [Fact]
    public void Nova_ComecaNaoRealizada()
    {
        var v = Nova();

        Assert.Equal(ResultadoVisita.NaoRealizada, v.Resultado);
        Assert.Null(v.RealizadaEm);
    }

    [Fact]
    public void Realizar_GuardaResultadoEObservacao()
    {
        var v = Nova();

        v.Realizar(T0.AddHours(1), ResultadoVisita.Orientado, "Orientada a beber água");

        Assert.Equal(ResultadoVisita.Orientado, v.Resultado);
        Assert.Equal(T0.AddHours(1), v.RealizadaEm);
    }

    [Fact]
    public void Realizar_DuasVezes_Lanca()
    {
        var v = Nova();
        v.Realizar(T0, ResultadoVisita.TudoBem);

        Assert.Throws<InvalidOperationException>(() => v.Realizar(T0, ResultadoVisita.TudoBem));
    }

    [Fact]
    public void Realizar_ComResultadoNaoRealizada_Lanca()
    {
        Assert.Throws<ArgumentException>(() => Nova().Realizar(T0, ResultadoVisita.NaoRealizada));
    }

    [Fact]
    public void Construtor_ComIdVazio_Lanca()
    {
        Assert.Throws<ArgumentException>(() => new Visita(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), T0));
    }
}

public class LeituraEOutrosTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Leitura_ComNivelCinza_Lanca()
    {
        Assert.Throws<ArgumentException>(
            () => new Leitura("esp32-001", T0, 30, 50, 31, NivelTermico.Cinza));
    }

    [Fact]
    public void ClimaExterno_MinimaMaiorQueMaxima_Lanca()
    {
        Assert.Throws<ArgumentException>(() => new ClimaExterno("A701", T0, 20, 25, 60));
    }

    [Fact]
    public void Dispositivo_NasceAtivoEPodeSerDesativado()
    {
        var d = new Dispositivo("esp32-001", Guid.NewGuid(), "hash");

        Assert.True(d.Ativo);
        d.Desativar();
        Assert.False(d.Ativo);
    }
}
