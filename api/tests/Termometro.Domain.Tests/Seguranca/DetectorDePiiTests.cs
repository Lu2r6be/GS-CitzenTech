using Termometro.Domain.Entidades;
using Termometro.Domain.Seguranca;

namespace Termometro.Domain.Tests.Seguranca;

public class DetectorDePiiTests
{
    private readonly DetectorDePii _detector = new();

    // CPFs de exemplo (dígitos verificadores corretos, de ninguém).
    private const string CpfValido = "529.982.247-25";
    private const string CpfValidoSemMascara = "52998224725";

    private CategoriaPii[] Categorias(string texto) => _detector.Detectar(texto).Select(o => o.Categoria).ToArray();

    [Theory]
    [InlineData(CpfValido)]
    [InlineData(CpfValidoSemMascara)]
    [InlineData("cpf do morador: 529.982.247-25, ok")]
    public void Cpf_ComDigitoVerificadorCorreto_EDetectado(string texto)
    {
        Assert.Contains(CategoriaPii.Cpf, Categorias(texto));
    }

    [Theory]
    [InlineData("529.982.247-26")]       // dígito errado
    [InlineData("111.111.111-11")]       // todos iguais
    [InlineData("12345678901")]          // 11 dígitos quaisquer
    [InlineData("protocolo 20261116123456789")] // número comprido (não tem exatamente 11 dígitos isolados)
    public void Cpf_Falso_NaoEDetectado(string texto)
    {
        Assert.DoesNotContain(CategoriaPii.Cpf, Categorias(texto));
    }

    // RGs de exemplo (inventados). RG só conta com rótulo: número solto não é acusado.
    [Theory]
    [InlineData("RG: 12.345.678-9")]
    [InlineData("rg 123456789")]
    [InlineData("identidade nº 12.345.678-X")]
    [InlineData("{\"rg\":\"12.345.678-9\"}")]
    [InlineData("Registro Geral: 1.234.567-8")]
    public void Rg_ComRotulo_EDetectado(string texto)
    {
        Assert.Contains(CategoriaPii.Documento, Categorias(texto));
    }

    [Theory]
    [InlineData("pedido 12.345.678-9")]            // número sem rótulo
    [InlineData("a rua tem 123456789 casas")]      // dígitos soltos
    [InlineData("sargento rg")]                    // rótulo sem número
    [InlineData("programa 12345678900 e 99")]      // parece CPF, não RG (e CPF inválido)
    public void Rg_SemRotulo_NaoEDetectado(string texto)
    {
        Assert.DoesNotContain(CategoriaPii.Documento, Categorias(texto));
    }

    [Fact]
    public void ChaveDocumento_ComCpfDentro_EClassificadaComoCpf_ENaoComoRg()
    {
        var categorias = Categorias("{\"documento\":\"529.982.247-25\"}");

        Assert.Contains(CategoriaPii.Cpf, categorias);
        Assert.DoesNotContain(CategoriaPii.Documento, categorias);
    }

    [Theory]
    [InlineData("senha: abc12345")]
    [InlineData("password=Segredo!9")]
    [InlineData("{\"senha\":\"Demo@2026!\"}")]
    [InlineData("login ok pwd = xyz789")]
    public void Senha_EmTexto_EDetectada(string texto)
    {
        Assert.Contains(CategoriaPii.Senha, Categorias(texto));
    }

    [Fact]
    public void Senha_JaTrocadaPorMarcador_NaoEDetectadaDeNovo()
    {
        Assert.Empty(_detector.Detectar("senha: [SENHA-REMOVIDA]"));
    }

    [Theory]
    [InlineData("morador com diabetes tipo 2")]
    [InlineData("Hipertensão arterial")]
    [InlineData("histórico de AVC em 2024")]
    [InlineData("insuficiência cardíaca")]
    [InlineData("CID-10: I10")]
    public void Saude_EDetectada(string texto)
    {
        Assert.Contains(CategoriaPii.Saude, Categorias(texto));
    }

    [Theory]
    [InlineData("Temperatura 33,4 C e umidade 61%")]
    [InlineData("visita realizada, tudo bem")]
    [InlineData("a asmara chegou")]                // "asma" dentro de outra palavra
    public void TextoComum_NaoTemPii(string texto)
    {
        Assert.Empty(_detector.Detectar(texto));
    }

    [Theory]
    [InlineData("Bearer abcdefghijklmnop12345")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abcdefghijk")]
    [InlineData("MISTRAL_API_KEY=sk-1234567890abcdef")]
    [InlineData("AKIAABCDEFGHIJKLMNOP")]
    public void Segredo_EDetectado(string texto)
    {
        Assert.Contains(CategoriaPii.Segredo, Categorias(texto));
    }

    [Fact]
    public void EmailTelefoneEEndereco_SaoDetectados()
    {
        var categorias = Categorias("ana@exemplo.com, (11) 98765-4321, Rua das Flores, 123");

        Assert.Contains(CategoriaPii.Email, categorias);
        Assert.Contains(CategoriaPii.Contato, categorias);
        Assert.Contains(CategoriaPii.Endereco, categorias);
    }

    [Fact]
    public void Ocorrencia_GuardaPosicaoENaoOValor()
    {
        var texto = "cpf " + CpfValido + " fim";

        var o = Assert.Single(_detector.Detectar(texto));

        Assert.Equal(4, o.Inicio);
        Assert.Equal(CpfValido.Length, o.Tamanho);
        Assert.DoesNotContain(CpfValido, o.ToString());
    }

    [Fact]
    public void TextoGigante_NaoTravaOMotor()
    {
        var texto = new string('9', 200_000) + " senha:" + new string('a', 10);

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        _ = _detector.Detectar(texto);

        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(5), $"demorou {relogio.Elapsed}");
    }

    [Fact]
    public void TextoNuloOuVazio_NaoTemPii()
    {
        Assert.Empty(_detector.Detectar(null));
        Assert.Empty(_detector.Detectar(""));
    }
}

public class PiiSanitizerTests
{
    private readonly PiiSanitizer _sanitizador = new();

    [Fact]
    public void Log_RemoveCpfSenhaESaude()
    {
        var limpo = _sanitizador.Sanitizar(
            "cadastro cpf 529.982.247-25 senha: abc12345 morador com diabetes", ExposicaoAchado.Log);

        Assert.DoesNotContain("247-25", limpo.Texto);
        Assert.DoesNotContain("abc12345", limpo.Texto);
        Assert.DoesNotContain("diabetes", limpo.Texto);
        Assert.Contains("***.982.247-**", limpo.Texto);
        Assert.Contains("[SENHA-REMOVIDA]", limpo.Texto);
        Assert.Equal(3, limpo.Ocorrencias.Count);
    }

    [Fact]
    public void Log_RemoveRg_ERespostaHttpTambem()
    {
        var emLog = _sanitizador.Sanitizar("morador RG: 12.345.678-9 cadastrado", ExposicaoAchado.Log);
        var emResposta = _sanitizador.Sanitizar("morador RG: 12.345.678-9 cadastrado", ExposicaoAchado.RespostaHttp);

        Assert.DoesNotContain("345.678", emLog.Texto);
        Assert.DoesNotContain("345.678", emResposta.Texto);
        Assert.Contains("[DOCUMENTO-REMOVIDO]", emLog.Texto);
    }

    [Fact]
    public void RespostaHttp_PermiteSaudeEEmail_MasNaoCpfNemSenha()
    {
        var limpo = _sanitizador.Sanitizar(
            "ana@exemplo.com tem diabetes, cpf 52998224725", ExposicaoAchado.RespostaHttp);

        Assert.Contains("ana@exemplo.com", limpo.Texto);
        Assert.Contains("diabetes", limpo.Texto);
        Assert.DoesNotContain("52998224725", limpo.Texto);
        Assert.Single(limpo.Ocorrencias);
    }

    [Fact]
    public void RespostaHttp_DeixaPassarOTokenDoLogin()
    {
        var token = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abcdefghijk";

        var limpo = _sanitizador.Sanitizar($"{{\"token\":\"{token}\"}}", ExposicaoAchado.RespostaHttp);

        Assert.Contains(token, limpo.Texto);
    }

    [Fact]
    public void PromptDoLlm_ProibeTudo()
    {
        var limpo = _sanitizador.Sanitizar("achado: ana@exemplo.com com diabetes", ExposicaoAchado.PromptLlm);

        Assert.DoesNotContain("ana@exemplo.com", limpo.Texto);
        Assert.DoesNotContain("diabetes", limpo.Texto);
    }

    [Theory]
    [InlineData("cpf 529.982.247-25 senha: abc12345 diabetes ana@exemplo.com (11) 98765-4321 Rua das Flores, 123")]
    [InlineData("nada sensivel aqui")]
    public void Sanitizar_DuasVezes_DaOMesmoResultado(string texto)
    {
        var uma = _sanitizador.Sanitizar(texto, ExposicaoAchado.Log);
        var duas = _sanitizador.Sanitizar(uma.Texto, ExposicaoAchado.Log);

        Assert.Equal(uma.Texto, duas.Texto);
        Assert.Empty(duas.Ocorrencias);
    }

    [Fact]
    public void TextoSemPii_VoltaIgual()
    {
        var limpo = _sanitizador.Sanitizar("Temperatura 33,4 C", ExposicaoAchado.Log);

        Assert.Equal("Temperatura 33,4 C", limpo.Texto);
        Assert.False(limpo.HouveSubstituicao);
    }
}

public class CalculadorDoRiscoCidadaoTests
{
    private static AchadoParaRisco Achado(SeveridadeAchado s, CategoriaPii? c, ExposicaoAchado e, string origem = "semgrep:regra") =>
        new(s, c, e, origem);

    [Fact]
    public void SemAchados_RiscoBaixoEZero()
    {
        var r = CalculadorDoRiscoCidadao.Calcular([]);

        Assert.Equal(0, r.Pontuacao);
        Assert.Equal(NivelDeRisco.Baixo, r.Nivel);
    }

    [Fact]
    public void FalhaAbertaNoCodigo_PesaMaisQueVazamentoBarradoEmExecucao()
    {
        var aberto = Achado(SeveridadeAchado.Alta, CategoriaPii.Cpf, ExposicaoAchado.Log, "semgrep:cpf-em-log");
        var barrado = Achado(SeveridadeAchado.Alta, CategoriaPii.Cpf, ExposicaoAchado.Log, "runtime:log-guard");

        Assert.True(CalculadorDoRiscoCidadao.PesoDe(aberto) > CalculadorDoRiscoCidadao.PesoDe(barrado));
        Assert.Equal(CalculadorDoRiscoCidadao.PesoDe(aberto) * 0.25, CalculadorDoRiscoCidadao.PesoDe(barrado), 6);
    }

    [Fact]
    public void SenhaNoPromptDoLlm_PesaMaisQueNomeNoLog()
    {
        var senha = Achado(SeveridadeAchado.Critica, CategoriaPii.Senha, ExposicaoAchado.PromptLlm);
        var nome = Achado(SeveridadeAchado.Baixa, CategoriaPii.Nome, ExposicaoAchado.Log);

        Assert.True(CalculadorDoRiscoCidadao.PesoDe(senha) > 20 * CalculadorDoRiscoCidadao.PesoDe(nome));
    }

    [Fact]
    public void Pontuacao_CrescComOsAchados_ENuncaPassaDe100()
    {
        var umPor = Achado(SeveridadeAchado.Critica, CategoriaPii.Senha, ExposicaoAchado.PromptLlm);
        var anterior = -1;
        foreach (var n in new[] { 0, 1, 3, 10, 100, 10_000 })
        {
            var r = CalculadorDoRiscoCidadao.Calcular(Enumerable.Repeat(umPor, n));
            Assert.InRange(r.Pontuacao, 0, 100);
            Assert.True(r.Pontuacao >= anterior);
            anterior = r.Pontuacao;
        }
        Assert.Equal(100, anterior);
    }

    [Theory]
    [InlineData(0, NivelDeRisco.Baixo)]
    [InlineData(19, NivelDeRisco.Baixo)]
    [InlineData(20, NivelDeRisco.Moderado)]
    [InlineData(49, NivelDeRisco.Moderado)]
    [InlineData(50, NivelDeRisco.Alto)]
    [InlineData(74, NivelDeRisco.Alto)]
    [InlineData(75, NivelDeRisco.Critico)]
    [InlineData(100, NivelDeRisco.Critico)]
    public void Faixas_DoNivel(int pontos, NivelDeRisco esperado)
    {
        Assert.Equal(esperado, CalculadorDoRiscoCidadao.NivelPara(pontos));
    }

    [Fact]
    public void ResumoSeparaAbertosDeBarrados_EAgrupaPorCategoriaESaida()
    {
        var r = CalculadorDoRiscoCidadao.Calcular(
        [
            Achado(SeveridadeAchado.Alta, CategoriaPii.Cpf, ExposicaoAchado.Log, "semgrep:a"),
            Achado(SeveridadeAchado.Alta, CategoriaPii.Cpf, ExposicaoAchado.Log, "runtime:log-guard"),
            Achado(SeveridadeAchado.Critica, CategoriaPii.Senha, ExposicaoAchado.RespostaHttp, "runtime:response-guard"),
        ]);

        Assert.Equal(3, r.TotalDeAchados);
        Assert.Equal(1, r.AbertosNoCodigo);
        Assert.Equal(2, r.BarradosEmExecucao);
        Assert.Contains(r.PorCategoria, g => g.Grupo == "Cpf" && g.Quantidade == 2);
        Assert.Contains(r.PorSaida, g => g.Grupo == "RespostaHttp" && g.Quantidade == 1);
    }

    [Theory]
    [InlineData(CategoriaPii.Senha, ExposicaoAchado.Log, SeveridadeAchado.Critica)]
    [InlineData(CategoriaPii.Cpf, ExposicaoAchado.Log, SeveridadeAchado.Alta)]
    [InlineData(CategoriaPii.Cpf, ExposicaoAchado.PromptLlm, SeveridadeAchado.Critica)]
    [InlineData(CategoriaPii.Documento, ExposicaoAchado.Log, SeveridadeAchado.Alta)]
    [InlineData(CategoriaPii.Documento, ExposicaoAchado.PromptLlm, SeveridadeAchado.Critica)]
    [InlineData(CategoriaPii.Email, ExposicaoAchado.Log, SeveridadeAchado.Media)]
    [InlineData(CategoriaPii.Nome, ExposicaoAchado.Log, SeveridadeAchado.Baixa)]
    public void SeveridadePadrao_DependeDoDadoEDaSaida(CategoriaPii c, ExposicaoAchado e, SeveridadeAchado esperado)
    {
        Assert.Equal(esperado, CalculadorDoRiscoCidadao.SeveridadePadrao(c, e));
    }
}
