using Termometro.Domain.Fila;
using Termometro.Domain.Niveis;

namespace Termometro.Domain.Tests.Fila;

public class OrdenadorDeFilaTests
{
    private readonly IOrdenadorDeFila _ordenador = new OrdenadorDeFila();

    private static readonly Vulnerabilidade Nenhuma = new(70, false, false);

    private static ItemFila Casa(NivelTermico nivel, Vulnerabilidade? vuln = null,
        TimeSpan? ultimaVisita = null) =>
        new(Guid.NewGuid(), nivel, vuln ?? Nenhuma, ultimaVisita);

    [Fact]
    public void Ordenar_SemDiaDeRisco_SegueSemSinalVermelhoAzulAmareloVerde()
    {
        var verde = Casa(NivelTermico.Verde);
        var amarelo = Casa(NivelTermico.Amarelo);
        var azul = Casa(NivelTermico.Azul);
        var vermelho = Casa(NivelTermico.Vermelho);
        var cinza = Casa(NivelTermico.Cinza);

        var fila = _ordenador.Ordenar([verde, amarelo, azul, vermelho, cinza], TipoDiaRisco.Nenhum);

        Assert.Equal([cinza, vermelho, azul, amarelo, verde], fila);
    }

    [Fact]
    public void Ordenar_SensorMudoVaiParaOTopoMesmoComVermelhoEMaisVulneravel()
    {
        var vermelhoVulneravel = Casa(NivelTermico.Vermelho, new Vulnerabilidade(90, true, true));
        var cinza = Casa(NivelTermico.Cinza);

        var fila = _ordenador.Ordenar([vermelhoVulneravel, cinza], TipoDiaRisco.Nenhum);

        Assert.Equal(cinza, fila[0]);
    }

    [Fact]
    public void Ordenar_EmDiaDeFrio_AzulEmpataComVermelhoNaPrioridade()
    {
        var amarelo = Casa(NivelTermico.Amarelo);
        var azulVulneravel = Casa(NivelTermico.Azul, new Vulnerabilidade(85, true, false));
        var vermelho = Casa(NivelTermico.Vermelho);

        var fila = _ordenador.Ordenar([amarelo, vermelho, azulVulneravel], TipoDiaRisco.Frio);

        // mesma faixa: o mais vulnerável vem primeiro
        Assert.Equal([azulVulneravel, vermelho, amarelo], fila);
    }

    [Fact]
    public void Ordenar_EmDiaDeCalor_AzulNaoGanhaPrioridade()
    {
        var azul = Casa(NivelTermico.Azul);
        var vermelho = Casa(NivelTermico.Vermelho);

        var fila = _ordenador.Ordenar([azul, vermelho], TipoDiaRisco.Calor);

        Assert.Equal([vermelho, azul], fila);
    }

    [Fact]
    public void Ordenar_MesmoNivel_MaisVulneravelPrimeiro()
    {
        var comum = Casa(NivelTermico.Amarelo, new Vulnerabilidade(70, false, false));
        var idosoSozinho = Casa(NivelTermico.Amarelo, new Vulnerabilidade(82, true, false));
        var tudo = Casa(NivelTermico.Amarelo, new Vulnerabilidade(90, true, true));

        var fila = _ordenador.Ordenar([comum, idosoSozinho, tudo], TipoDiaRisco.Nenhum);

        Assert.Equal([tudo, idosoSozinho, comum], fila);
    }

    [Fact]
    public void Ordenar_MesmoNivelEVulnerabilidade_QuemEstaHaMaisTempoSemVisitaPrimeiro()
    {
        var visitadaOntem = Casa(NivelTermico.Verde, ultimaVisita: TimeSpan.FromDays(1));
        var visitadaHaUmaSemana = Casa(NivelTermico.Verde, ultimaVisita: TimeSpan.FromDays(7));
        var nuncaVisitada = Casa(NivelTermico.Verde, ultimaVisita: null);

        var fila = _ordenador.Ordenar(
            [visitadaOntem, nuncaVisitada, visitadaHaUmaSemana], TipoDiaRisco.Nenhum);

        Assert.Equal([nuncaVisitada, visitadaHaUmaSemana, visitadaOntem], fila);
    }

    [Fact]
    public void Ordenar_MesmaEntradaEmOutraOrdem_DaAMesmaFila()
    {
        var itens = Enumerable.Range(0, 6)
            .Select(i => Casa(NivelTermico.Amarelo, ultimaVisita: TimeSpan.FromDays(i % 2)))
            .ToList();

        var fila1 = _ordenador.Ordenar(itens, TipoDiaRisco.Nenhum);
        var fila2 = _ordenador.Ordenar(Enumerable.Reverse(itens), TipoDiaRisco.Nenhum);

        Assert.Equal(fila1, fila2);
    }

    [Fact]
    public void Vulnerabilidade_ContaIdadeDe80SolidaoEDoencaCronica()
    {
        Assert.Equal(0, new Vulnerabilidade(79, false, false).Pontos);
        Assert.Equal(1, new Vulnerabilidade(80, false, false).Pontos);
        Assert.Equal(3, new Vulnerabilidade(85, true, true).Pontos);
    }
}
