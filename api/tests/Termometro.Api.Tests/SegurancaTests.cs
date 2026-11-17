using System.Net;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Termometro.Api.Seguranca;
using Termometro.Application.Seguranca;
using Termometro.Domain.Entidades;
using Termometro.Domain.Seguranca;
using Termometro.Infrastructure.Persistencia;

namespace Termometro.Api.Tests;

// CPF e senha de exemplo (fictícios). O CPF tem dígitos verificadores corretos, de ninguém.
internal static class Ficticios
{
    public const string Cpf = "529.982.247-25";
    public const string CpfSoNumeros = "52998224725";
    public const string Senha = "abc12345";
}

[CollectionDefinition("Console", DisableParallelization = true)]
public sealed class ColecaoDoConsole;

public class RiscoCidadaoApiTests
{
    private static async Task<JsonElement> LerAsync(HttpResponseMessage r) =>
        await r.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task SemAchados_PontuacaoZeroEAlertaDeReserva()
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();

        var resposta = await admin.GetAsync("/api/v1/seguranca/risco-cidadao");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var json = await LerAsync(resposta);
        Assert.Equal(0, json.GetProperty("pontuacao").GetInt32());
        Assert.Equal("Baixo", json.GetProperty("nivel").GetString());
        Assert.Equal("modelo-reserva", json.GetProperty("alerta").GetProperty("fonte").GetString());
    }

    [Fact]
    public async Task AcsEFamiliar_NaoVemORiscoCidadao()
    {
        await using var api = new FabricaDaApi();

        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.AcsAsync()).GetAsync("/api/v1/seguranca/risco-cidadao")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.FamiliarAsync()).GetAsync("/api/v1/seguranca/risco-cidadao")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/v1/seguranca/risco-cidadao")).StatusCode);
    }

    [Fact]
    public async Task ImportarAchados_AumentaOIndicador()
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();

        var importacao = await admin.PostAsJsonAsync("/api/v1/seguranca/achados", new
        {
            achados = new[]
            {
                new { tipo = "cpf-em-log", severidade = "alta", origem = "semgrep:pii-para-log", exposicao = "log", categoriaPii = "cpf" },
                new { tipo = "senha-em-resposta", severidade = "critica", origem = "semgrep:pii-para-resposta", exposicao = "respostaHttp", categoriaPii = "senha" },
            },
        });

        Assert.Equal(HttpStatusCode.OK, importacao.StatusCode);
        var painel = await LerAsync(await admin.GetAsync("/api/v1/seguranca/risco-cidadao"));
        Assert.True(painel.GetProperty("pontuacao").GetInt32() > 0);
        Assert.Equal(2, painel.GetProperty("totalDeAchados").GetInt32());
        Assert.Equal(2, painel.GetProperty("abertosNoCodigo").GetInt32());
        Assert.Equal(0, painel.GetProperty("barradosEmExecucao").GetInt32());
    }

    [Theory]
    [InlineData("tipo com 529.982.247-25 dentro", "semgrep:x")]
    [InlineData("tipo-ok", "origem com senha: abc12345")]
    public async Task ImportarAchados_ComDadoSensivelNoTexto_Da400ENaoRepeteODado(string tipo, string origem)
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();

        var resposta = await admin.PostAsJsonAsync("/api/v1/seguranca/achados", new
        {
            achados = new[] { new { tipo, severidade = "alta", origem, exposicao = "log", categoriaPii = "cpf" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("247-25", corpo);
        Assert.DoesNotContain("abc12345", corpo);
    }

    [Theory]
    [InlineData("alta", "log", "categoria-inexistente")]
    [InlineData("gravissima", "log", "cpf")]
    [InlineData("alta", "telepatia", "cpf")]
    public async Task ImportarAchados_ValoresInvalidos_Da400(string severidade, string exposicao, string categoria)
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();

        var resposta = await admin.PostAsJsonAsync("/api/v1/seguranca/achados", new
        {
            achados = new[] { new { tipo = "x", severidade, origem = "semgrep:y", exposicao, categoriaPii = categoria } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task ImportarAchados_LoteVazioOuGrande_Da400()
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/v1/seguranca/achados", new { achados = Array.Empty<object>() })).StatusCode);

        var grande = Enumerable.Range(0, 501).Select(_ => new { tipo = "x", severidade = "baixa", origem = "semgrep:y", exposicao = "log", categoriaPii = "nome" });
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/v1/seguranca/achados", new { achados = grande })).StatusCode);
    }

    private static object Item(string origem, string tipo = "pii-cpf-para-log") =>
        new { tipo, severidade = "alta", origem, exposicao = "log", categoriaPii = "cpf" };

    [Fact]
    public async Task Substituir_ApagaAVarreduraAnteriorDaMesmaFerramenta_EManteOQueFoiBarradoEmExecucao()
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();
        await admin.PostAsJsonAsync("/api/v1/seguranca/achados", new { achados = new[] { Item("semgrep:a"), Item("semgrep:b"), Item("trufflehog:c") } });
        await admin.PostAsJsonAsync("/api/v1/seguranca/analisar-texto", new { texto = "x", saida = "log" });

        var troca = await admin.PostAsJsonAsync("/api/v1/seguranca/achados",
            new { achados = new[] { Item("semgrep:novo") }, substituirOrigem = "semgrep:" });

        Assert.Equal(HttpStatusCode.OK, troca.StatusCode);
        var json = await LerAsync(troca);
        Assert.Equal(1, json.GetProperty("gravados").GetInt32());
        Assert.Equal(2, json.GetProperty("substituidos").GetInt32());
        var painel = await LerAsync(await admin.GetAsync("/api/v1/seguranca/risco-cidadao"));
        Assert.Equal(2, painel.GetProperty("totalDeAchados").GetInt32()); // semgrep:novo + trufflehog:c
    }

    [Fact]
    public async Task Substituir_ComListaVazia_LimpaAVarredura()
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();
        await admin.PostAsJsonAsync("/api/v1/seguranca/achados", new { achados = new[] { Item("semgrep:a") } });

        var limpa = await admin.PostAsJsonAsync("/api/v1/seguranca/achados",
            new { achados = Array.Empty<object>(), substituirOrigem = "semgrep:" });

        Assert.Equal(HttpStatusCode.OK, limpa.StatusCode);
        var painel = await LerAsync(await admin.GetAsync("/api/v1/seguranca/risco-cidadao"));
        Assert.Equal(0, painel.GetProperty("totalDeAchados").GetInt32());
        Assert.Equal(0, painel.GetProperty("pontuacao").GetInt32());
    }

    [Theory]
    [InlineData("runtime:")]
    [InlineData("qualquer")]
    public async Task Substituir_ComPrefixoNaoPermitido_Da400(string prefixo)
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();

        var resposta = await admin.PostAsJsonAsync("/api/v1/seguranca/achados",
            new { achados = Array.Empty<object>(), substituirOrigem = prefixo });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Substituir_ComOrigemDeOutraFerramentaNoLote_Da400_ENaoApagaNada()
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();
        await admin.PostAsJsonAsync("/api/v1/seguranca/achados", new { achados = new[] { Item("semgrep:a") } });

        var resposta = await admin.PostAsJsonAsync("/api/v1/seguranca/achados",
            new { achados = new[] { Item("runtime:fingido") }, substituirOrigem = "semgrep:" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var painel = await LerAsync(await admin.GetAsync("/api/v1/seguranca/risco-cidadao"));
        Assert.Equal(1, painel.GetProperty("totalDeAchados").GetInt32());
    }

    [Fact]
    public async Task Substituir_ComItemInvalido_Da400_ENaoApagaAVarreduraAnterior()
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();
        await admin.PostAsJsonAsync("/api/v1/seguranca/achados", new { achados = new[] { Item("semgrep:a") } });

        var resposta = await admin.PostAsJsonAsync("/api/v1/seguranca/achados", new
        {
            achados = new[] { new { tipo = "x", severidade = "gravissima", origem = "semgrep:b", exposicao = "log", categoriaPii = "cpf" } },
            substituirOrigem = "semgrep:",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var painel = await LerAsync(await admin.GetAsync("/api/v1/seguranca/risco-cidadao"));
        Assert.Equal(1, painel.GetProperty("totalDeAchados").GetInt32());
    }

    [Fact]
    public async Task AnalisarTexto_MostraOndeEstaOPiiSemDevolverOValor()
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();

        var resposta = await admin.PostAsJsonAsync("/api/v1/seguranca/analisar-texto",
            new { texto = $"cpf {Ficticios.Cpf} senha: {Ficticios.Senha} morador com diabetes", saida = "log" });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("247-25", corpo);
        Assert.DoesNotContain(Ficticios.Senha, corpo);
        Assert.DoesNotContain("diabetes", corpo);
        var json = JsonDocument.Parse(corpo).RootElement;
        var categorias = json.GetProperty("ocorrencias").EnumerateArray().Select(o => o.GetProperty("categoria").GetString()).ToList();
        Assert.Contains("Cpf", categorias);
        Assert.Contains("Senha", categorias);
        Assert.Contains("Saude", categorias);
    }

    [Fact]
    public async Task Login_ContinuaDevolvendoOToken_ComOGuardaDeRespostaLigado()
    {
        await using var api = new FabricaDaApi();

        var resposta = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@demo.local", senha = FabricaDaApi.Senha });

        var json = await LerAsync(resposta);
        Assert.StartsWith("eyJ", json.GetProperty("token").GetString());
    }
}

[Collection("Console")]
public class GuardasEmExecucaoTests
{
    private static async Task<List<AchadoSeguranca>> EsperarAchadosAsync(FabricaDaApi api, Func<AchadoSeguranca, bool> filtro, int minimo)
    {
        for (var i = 0; i < 100; i++)
        {
            await using var escopo = api.Services.CreateAsyncScope();
            var banco = escopo.ServiceProvider.GetRequiredService<TermometroDbContext>();
            var achados = (await banco.Achados.AsNoTracking().ToListAsync()).Where(filtro).ToList();
            if (achados.Count >= minimo)
            {
                return achados;
            }
            await Task.Delay(50);
        }
        return [];
    }

    [Fact]
    public async Task SimularVazamento_LogSaiLimpo_RespostaSaiMascarada_EOsDoisViramAchados()
    {
        var antes = Console.Out;
        var captura = new StringWriter();
        Console.SetOut(captura);
        try
        {
            await using var api = new FabricaDaApi();
            var admin = await api.AdminAsync();
            var texto = $"cadastro cpf {Ficticios.Cpf} senha: {Ficticios.Senha} morador com diabetes";

            var resposta = await admin.PostAsJsonAsync("/api/v1/seguranca/simular-vazamento", new { texto });

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await resposta.Content.ReadAsStringAsync();
            // Resposta: CPF mascarado e senha removida; saúde pode (política da resposta).
            Assert.DoesNotContain("247-25", corpo);
            Assert.DoesNotContain(Ficticios.Senha, corpo);
            Assert.Contains("***.982.247-**", corpo);
            Assert.Contains("diabetes", corpo);

            // Log: nada disso passou para o console.
            var log = captura.ToString();
            Assert.Contains("Texto de teste recebido", log);
            Assert.DoesNotContain("247-25", log);
            Assert.DoesNotContain(Ficticios.Senha, log);
            Assert.DoesNotContain("diabetes", log);

            var doLog = await EsperarAchadosAsync(api, a => a.Origem == "runtime:log-guard", 3);
            Assert.Contains(doLog, a => a.CategoriaPii == CategoriaPii.Cpf && a.Exposicao == ExposicaoAchado.Log);
            Assert.Contains(doLog, a => a.CategoriaPii == CategoriaPii.Senha);
            Assert.Contains(doLog, a => a.CategoriaPii == CategoriaPii.Saude);

            var daResposta = await EsperarAchadosAsync(api, a => a.Origem == "runtime:response-guard", 2);
            Assert.Contains(daResposta, a => a.CategoriaPii == CategoriaPii.Cpf && a.Exposicao == ExposicaoAchado.RespostaHttp);
            Assert.Contains(daResposta, a => a.CategoriaPii == CategoriaPii.Senha);
            Assert.DoesNotContain(daResposta, a => a.CategoriaPii == CategoriaPii.Saude);

            // O painel agora mostra risco barrado em execução.
            var painel = await admin.GetFromJsonAsync<JsonElement>("/api/v1/seguranca/risco-cidadao");
            Assert.True(painel.GetProperty("barradosEmExecucao").GetInt32() >= 5);
            Assert.Equal(0, painel.GetProperty("abertosNoCodigo").GetInt32());
        }
        finally
        {
            Console.SetOut(antes);
        }
    }

    [Fact]
    public async Task ErroInesperado_NaoVazaDadoDaExcecaoNoLog()
    {
        var antes = Console.Out;
        var captura = new StringWriter();
        Console.SetOut(captura);
        try
        {
            await using var api = new FabricaDaApi();
            var logger = api.Services.GetRequiredService<ILoggerFactory>().CreateLogger("teste");

            logger.LogError(new InvalidOperationException($"CPF {Ficticios.Cpf} inválido"), "falha ao salvar");

            Assert.DoesNotContain("247-25", captura.ToString());
            Assert.Contains("falha ao salvar", captura.ToString());
        }
        finally
        {
            Console.SetOut(antes);
        }
    }
}

public class TradutorDeAlertaTests
{
    private static readonly ResumoParaAlerta Resumo = new(
        62, NivelDeRisco.Alto, 7, 4, 3,
        [new ContribuicaoDoGrupo("Cpf", 4, 20), new ContribuicaoDoGrupo("Senha", 3, 30)],
        [new ContribuicaoDoGrupo("Log", 5, 30), new ContribuicaoDoGrupo("RespostaHttp", 2, 20)]);

    private sealed class ManipuladorFalso(Func<HttpRequestMessage, HttpResponseMessage> resposta) : HttpMessageHandler
    {
        public List<string> Corpos { get; } = [];
        public HttpRequestMessage? Ultimo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            Ultimo = pedido;
            Corpos.Add(pedido.Content is null ? "" : await pedido.Content.ReadAsStringAsync(ct));
            return resposta(pedido);
        }
    }

    private static HttpResponseMessage Ok(string alertaJson) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = alertaJson } } } }),
            Encoding.UTF8, "application/json"),
    };

    private static TradutorMistral Mistral(ManipuladorFalso m, string chave = "chave-de-teste-1234567890") =>
        new(new HttpClient(m), Microsoft.Extensions.Options.Options.Create(new OpcoesMistral { ApiKey = chave }));

    [Fact]
    public async Task Mistral_EnviaChaveEPromptSemDadoPessoal_ELeOAlerta()
    {
        var manipulador = new ManipuladorFalso(_ => Ok("""{"titulo":"Seus dados correm risco","mensagem":"Falhas expuseram dados.","acoes":["Desconfie de golpes","Procure a unidade"]}"""));

        var alerta = await Mistral(manipulador).TraduzirAsync(Resumo, CancellationToken.None);

        Assert.Equal("mistral", alerta.Fonte);
        Assert.Equal("Seus dados correm risco", alerta.Titulo);
        Assert.Equal(2, alerta.Acoes.Count);
        Assert.Equal("Bearer chave-de-teste-1234567890", manipulador.Ultimo!.Headers.Authorization!.ToString());
        Assert.False(new DetectorDePii().ContemPii(manipulador.Corpos[0].Replace("chave-de-teste-1234567890", "")));
    }

    [Fact]
    public async Task Prompt_DizSeAsTentativasForamBarradasOuSeHaFalhaAberta()
    {
        var soBarrados = Resumo with { TotalDeAchados = 5, AbertosNoCodigo = 0, BarradosEmExecucao = 5 };
        var m1 = new ManipuladorFalso(_ => Ok("""{"titulo":"T","mensagem":"M","acoes":["a"]}"""));
        await Mistral(m1).TraduzirAsync(soBarrados, CancellationToken.None);
        Assert.Contains("nenhum dado chegou a ser exposto", m1.Corpos[0]);

        var m2 = new ManipuladorFalso(_ => Ok("""{"titulo":"T","mensagem":"M","acoes":["a"]}"""));
        await Mistral(m2).TraduzirAsync(Resumo, CancellationToken.None);
        Assert.Contains("ainda precisam ser corrigidas", m2.Corpos[0]);
        Assert.DoesNotContain("nenhum dado chegou a ser exposto", m2.Corpos[0]);
    }

    [Fact]
    public async Task SemChave_NemTentaChamar_EAReservaAssume()
    {
        var manipulador = new ManipuladorFalso(_ => Ok("{}"));
        var tradutor = new TradutorComReserva(Mistral(manipulador, chave: ""), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TradutorComReserva>.Instance);

        var alerta = await tradutor.TraduzirAsync(Resumo, CancellationToken.None);

        Assert.Equal("modelo-reserva", alerta.Fonte);
        Assert.Empty(manipulador.Corpos);
    }

    [Fact]
    public async Task ErroDeRedeOu500_UsaReserva()
    {
        var tradutor = new TradutorComReserva(
            Mistral(new ManipuladorFalso(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError))),
            TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<TradutorComReserva>.Instance);

        var alerta = await tradutor.TraduzirAsync(Resumo, CancellationToken.None);

        Assert.Equal("modelo-reserva", alerta.Fonte);
        Assert.Contains("62 de 100", alerta.Mensagem);
    }

    [Fact]
    public async Task AlertaDaIaComCpf_EDescartado_UsaReserva()
    {
        var json = """{"titulo":"Alerta","mensagem":"O CPF 529.982.247-25 vazou.","acoes":["a"]}""";
        var tradutor = new TradutorComReserva(Mistral(new ManipuladorFalso(_ => Ok(json))),
            TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<TradutorComReserva>.Instance);

        var alerta = await tradutor.TraduzirAsync(Resumo, CancellationToken.None);

        Assert.Equal("modelo-reserva", alerta.Fonte);
        Assert.DoesNotContain("247-25", alerta.Mensagem);
    }

    [Fact]
    public async Task RespostaVaziaOuIlegivel_UsaReserva()
    {
        foreach (var conteudo in new[] { "nao e json", """{"titulo":"","mensagem":"","acoes":[]}""" })
        {
            var tradutor = new TradutorComReserva(Mistral(new ManipuladorFalso(_ => Ok(conteudo))),
                TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<TradutorComReserva>.Instance);

            Assert.Equal("modelo-reserva", (await tradutor.TraduzirAsync(Resumo, CancellationToken.None)).Fonte);
        }
    }

    [Fact]
    public async Task MesmoResumo_NaoChamaAIaDeNovoDentroDoCache()
    {
        var manipulador = new ManipuladorFalso(_ => Ok("""{"titulo":"T","mensagem":"M","acoes":["a"]}"""));
        var tradutor = new TradutorComReserva(Mistral(manipulador), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TradutorComReserva>.Instance);

        await tradutor.TraduzirAsync(Resumo, CancellationToken.None);
        await tradutor.TraduzirAsync(Resumo, CancellationToken.None);

        Assert.Single(manipulador.Corpos);
    }

    [Theory]
    [InlineData(NivelDeRisco.Baixo)]
    [InlineData(NivelDeRisco.Moderado)]
    [InlineData(NivelDeRisco.Alto)]
    [InlineData(NivelDeRisco.Critico)]
    public async Task Reserva_NuncaDevolveTextoComPii_ParaQualquerNivel(NivelDeRisco nivel)
    {
        var resumo = Resumo with { Nivel = nivel };

        var alerta = await new TradutorDeReserva().TraduzirAsync(resumo, CancellationToken.None);

        Assert.False(new DetectorDePii().ContemPii(alerta.Titulo + alerta.Mensagem + string.Join(' ', alerta.Acoes)));
        Assert.NotEmpty(alerta.Acoes);
    }
}
