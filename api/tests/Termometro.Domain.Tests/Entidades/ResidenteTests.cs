using Termometro.Domain.Entidades;
using Termometro.Domain.Seguranca;

namespace Termometro.Domain.Tests.Entidades;

public class ResidenteTests
{
    private static Residente Criar(DateOnly nascimento, bool sozinho = false, bool cronica = false) =>
        new("Maria da Silva", Cpf.Criar("529.982.247-25"), nascimento, sozinho, cronica);

    [Fact]
    public void IdadeEm_NoDiaDoAniversario_JaFezAnos()
    {
        var r = Criar(new DateOnly(1940, 6, 15));

        Assert.Equal(85, r.IdadeEm(new DateOnly(2026, 6, 14)));
        Assert.Equal(86, r.IdadeEm(new DateOnly(2026, 6, 15)));
    }

    [Fact]
    public void VulnerabilidadeEm_JuntaIdadeSolidaoEDoenca()
    {
        var r = Criar(new DateOnly(1940, 6, 15), sozinho: true);

        var v = r.VulnerabilidadeEm(new DateOnly(2026, 11, 13));

        Assert.Equal(86, v.Idade);
        Assert.Equal(2, v.Pontos); // 80+ e mora sozinha
    }

    [Fact]
    public void Construtor_NomeVazio_Lanca()
    {
        Assert.Throws<ArgumentException>(
            () => new Residente(" ", Cpf.Criar("52998224725"), new DateOnly(1940, 1, 1), false, false));
    }

    [Fact]
    public void Construtor_SemDataDeNascimento_Lanca()
    {
        Assert.Throws<ArgumentException>(
            () => new Residente("Maria", Cpf.Criar("52998224725"), default, false, false));
    }

    [Fact]
    public void CamposComDadoPessoal_EstaoMarcadosComoPii()
    {
        static CategoriaPii? Categoria<T>(string propriedade) =>
            typeof(T).GetProperty(propriedade)?.GetCustomAttributes(typeof(PiiAttribute), false)
                .Cast<PiiAttribute>().SingleOrDefault()?.Categoria;

        Assert.Equal(CategoriaPii.Nome, Categoria<Residente>(nameof(Residente.Nome)));
        Assert.Equal(CategoriaPii.Cpf, Categoria<Residente>(nameof(Residente.Cpf)));
        Assert.Equal(CategoriaPii.Saude, Categoria<Residente>(nameof(Residente.CondicoesSaude)));
        Assert.Equal(CategoriaPii.Endereco, Categoria<Domicilio>(nameof(Domicilio.Endereco)));
        Assert.Equal(CategoriaPii.Senha, Categoria<Usuario>(nameof(Usuario.SenhaHash)));
        Assert.Equal(CategoriaPii.Email, Categoria<Usuario>(nameof(Usuario.Email)));
    }
}
