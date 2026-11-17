using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Termometro.Domain.Entidades;
using Termometro.Infrastructure.Persistencia;
using static Termometro.Api.Tests.FabricaDaApi;

namespace Termometro.Api.Tests;

public class AutenticacaoTests
{
    [Fact]
    public async Task Login_ComSenhaCorreta_DevolveTokenEPapel()
    {
        await using var api = new FabricaDaApi();

        var resposta = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "ana.acs@demo.local", senha = Senha });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var json = await LerJsonAsync(resposta);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("token").GetString()));
        Assert.Equal("Acs", json.GetProperty("papel").GetString());
    }

    [Theory]
    [InlineData("ana.acs@demo.local", "senha-errada")]
    [InlineData("naoexiste@demo.local", Senha)]
    public async Task Login_Invalido_Da401(string email, string senha)
    {
        await using var api = new FabricaDaApi();

        var resposta = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, senha });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task RotaProtegida_SemToken_Da401()
    {
        await using var api = new FabricaDaApi();

        var resposta = await api.CreateClient().GetAsync("/api/v1/visitas/fila");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Saude_EOSwagger_RespondemSemLogin()
    {
        await using var api = new FabricaDaApi();
        var cliente = api.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }
}

public class TelemetriaTests
{
    [Fact]
    public async Task Telemetria_Valida_Da202ComONivel()
    {
        await using var api = new FabricaDaApi();

        var nivel = await api.NivelAposLeituraAsync("esp32-001", 25, 50);

        Assert.Equal("verde", nivel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("chave-errada")]
    public async Task Telemetria_ChaveAusenteOuErrada_Da401(string? chave)
    {
        await using var api = new FabricaDaApi();

        var resposta = await api.EnviarLeituraAsync("esp32-001", 25, 50, chave);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Telemetria_DispositivoInexistente_Da401ComAMesmaMensagemDaChaveErrada()
    {
        await using var api = new FabricaDaApi();

        var inexistente = await LerJsonAsync(await api.EnviarLeituraAsync("fantasma", 25, 50));
        var chaveErrada = await LerJsonAsync(await api.EnviarLeituraAsync("esp32-001", 25, 50, "errada"));

        // Mensagens iguais: quem ataca não consegue descobrir quais ids de dispositivo existem.
        Assert.Equal(inexistente.GetProperty("detail").GetString(), chaveErrada.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData(25, 150)]
    [InlineData(25, -1)]
    [InlineData(200, 50)]
    [InlineData(-60, 50)]
    public async Task Telemetria_ForaDosLimitesDoSensor_Da400(double temperatura, double umidade)
    {
        await using var api = new FabricaDaApi();

        var resposta = await api.EnviarLeituraAsync("esp32-001", temperatura, umidade);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Telemetria_SemTemperatura_Da400EmVezDeVirarZeroGrau()
    {
        await using var api = new FabricaDaApi();
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/telemetria")
        {
            Content = JsonContent.Create(new { deviceId = "esp32-001", umidade = 50 }),
        };
        requisicao.Headers.Add("X-Device-Key", ChaveDoDispositivo);

        var resposta = await api.CreateClient().SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Telemetria_PrimeiraLeituraAdotaONivel_MasMudarExigeTresSeguidas()
    {
        await using var api = new FabricaDaApi();

        var niveis = new List<string>
        {
            await api.LerEAvancarAsync("esp32-001", 25, 50),  // 1ª leitura: adota verde
            await api.LerEAvancarAsync("esp32-001", 40, 50),  // vermelho (1ª)
            await api.LerEAvancarAsync("esp32-001", 40, 50),  // vermelho (2ª)
            await api.LerEAvancarAsync("esp32-001", 40, 50),  // vermelho (3ª): confirma
        };

        Assert.Equal(["verde", "verde", "verde", "vermelho"], niveis);
    }

    [Fact]
    public async Task Telemetria_LeituraIsoladaDiferente_NaoFazOSemaforoPiscar()
    {
        await using var api = new FabricaDaApi();
        await api.LerEAvancarAsync("esp32-001", 25, 50);
        await api.LerEAvancarAsync("esp32-001", 40, 50); // um pico
        var nivel = await api.LerEAvancarAsync("esp32-001", 25, 50); // voltou ao normal

        Assert.Equal("verde", nivel);
    }

    [Fact]
    public async Task Limiares_SaoPublicosEComecamNasFaixasIniciais()
    {
        await using var api = new FabricaDaApi();

        var json = await LerJsonAsync(await api.CreateClient().GetAsync("/api/v1/config/limiares"));

        Assert.Equal(18, json.GetProperty("azulAbaixoDe").GetDouble());
        Assert.Equal(32, json.GetProperty("verdeAte").GetDouble());
        Assert.Equal(39, json.GetProperty("amareloAte").GetDouble());
    }
}

public class FilaTests
{
    private static async Task<List<JsonElement>> CasasDaFilaAsync(HttpClient acs)
    {
        var json = await LerJsonAsync(await acs.GetAsync("/api/v1/visitas/fila"));
        return json.GetProperty("casas").EnumerateArray().ToList();
    }

    private static string[] Nomes(IEnumerable<JsonElement> casas) =>
        casas.Select(c => c.GetProperty("nomeResidente").GetString()!.Split(' ')[0]).ToArray();

    [Fact]
    public async Task Fila_SemNenhumaLeitura_TemTodasAsCasasEmCinza()
    {
        await using var api = new FabricaDaApi();
        var acs = await api.AcsAsync();

        var casas = await CasasDaFilaAsync(acs);

        Assert.Equal(5, casas.Count);
        Assert.All(casas, c => Assert.Equal("cinza", c.GetProperty("nivel").GetString()));
    }

    [Fact]
    public async Task Fila_OrdenaPorUrgencia_EDesempataPorVulnerabilidade()
    {
        await using var api = new FabricaDaApi();
        await api.LerEAvancarAsync("esp32-001", 25, 50); // Maria (a avó): verde, 3 pontos de vulnerabilidade
        await api.LerEAvancarAsync("sim-002", 40, 50);   // José: vermelho
        await api.LerEAvancarAsync("sim-003", 33, 50);   // Antônia: amarelo
        await api.LerEAvancarAsync("sim-004", 25, 50);   // Benedito: verde, 0 pontos
        await api.LerEAvancarAsync("sim-005", 15, 50);   // Célia: azul
        var acs = await api.AcsAsync();

        var ordem = Nomes(await CasasDaFilaAsync(acs));

        // vermelho > azul > amarelo > verde; entre os dois verdes, a avó (mais vulnerável) vem antes.
        Assert.Equal(["José", "Célia", "Antônia", "Maria", "Benedito"], ordem);
    }

    [Fact]
    public async Task Fila_SensorMudoPor30Minutos_VaiParaOTopoEmCinza()
    {
        await using var api = new FabricaDaApi();
        foreach (var d in new[] { "esp32-001", "sim-002", "sim-003", "sim-004", "sim-005" })
        {
            await api.NivelAposLeituraAsync(d, 25, 50);
        }
        // Só a casa do José continua enviando; o resto fica mudo.
        api.Relogio.Avancar(TimeSpan.FromMinutes(31));
        await api.NivelAposLeituraAsync("sim-002", 25, 50);
        var acs = await api.AcsAsync();

        var casas = await CasasDaFilaAsync(acs);

        Assert.Equal("verde", casas.Last().GetProperty("nivel").GetString());
        Assert.Equal("José", Nomes(casas).Last());
        Assert.Equal(4, casas.Count(c => c.GetProperty("semSinal").GetBoolean()));
        Assert.Equal("cinza", casas[0].GetProperty("nivel").GetString());
    }

    [Fact]
    public async Task Fila_EmDiaDeFrio_AzulPassaParaAPrioridadeDoVermelho()
    {
        await using var api = new FabricaDaApi();
        await api.LerEAvancarAsync("sim-004", 40, 50);  // Benedito: vermelho, 0 pontos
        await api.LerEAvancarAsync("sim-005", 15, 50);  // Célia: azul, 1 ponto
        foreach (var d in new[] { "esp32-001", "sim-002", "sim-003" })
        {
            await api.LerEAvancarAsync(d, 25, 50);
        }
        var acs = await api.AcsAsync();
        var diaNormal = Nomes(await CasasDaFilaAsync(acs)).Take(2).ToArray();

        // Chega o clima externo: mínima de 10 °C (≤ 13 °C é dia de frio no ML).
        await using (var escopo = api.Services.CreateAsyncScope())
        {
            var banco = escopo.ServiceProvider.GetRequiredService<TermometroDbContext>();
            banco.ClimasExternos.Add(new ClimaExterno("A701", api.Relogio.GetUtcNow(), 22, 10, 70));
            await banco.SaveChangesAsync();
        }
        var json = await LerJsonAsync(await acs.GetAsync("/api/v1/visitas/fila"));
        var diaDeFrio = Nomes(json.GetProperty("casas").EnumerateArray()).Take(2).ToArray();

        Assert.Equal("frio", json.GetProperty("diaDeRisco").GetString());
        Assert.Equal(["Benedito", "Célia"], diaNormal); // vermelho antes do azul
        Assert.Equal(["Célia", "Benedito"], diaDeFrio); // mesma faixa: Célia é mais vulnerável
    }

    [Fact]
    public async Task Fila_FamiliarEAdmin_NaoTemAcesso()
    {
        await using var api = new FabricaDaApi();

        var familiar = await (await api.FamiliarAsync()).GetAsync("/api/v1/visitas/fila");
        var admin = await (await api.AdminAsync()).GetAsync("/api/v1/visitas/fila");

        Assert.Equal(HttpStatusCode.Forbidden, familiar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
    }
}

public class CasasETestesDePrivacidade
{
    private static async Task<(Guid Avo, Guid Outra)> IdsDasCasasAsync(HttpClient acs)
    {
        var fila = await LerJsonAsync(await acs.GetAsync("/api/v1/visitas/fila"));
        var casas = fila.GetProperty("casas").EnumerateArray().ToList();
        Guid Id(string nome) => casas.First(c => c.GetProperty("nomeResidente").GetString()!.StartsWith(nome))
            .GetProperty("domicilioId").GetGuid();
        return (Id("Maria"), Id("José"));
    }

    [Fact]
    public async Task Detalhe_NuncaTrazOCpfCompleto()
    {
        await using var api = new FabricaDaApi();
        await api.LerEAvancarAsync("esp32-001", 25, 50);
        var acs = await api.AcsAsync();
        var (avo, _) = await IdsDasCasasAsync(acs);

        var resposta = await acs.GetAsync($"/api/v1/domicilios/{avo}");
        var texto = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("***.982.247-**", texto);
        Assert.DoesNotContain("52998224725", texto);
        Assert.DoesNotContain("529.982.247-25", texto);
    }

    [Fact]
    public async Task Detalhe_MostraAsUltimasLeiturasEATendencia()
    {
        await using var api = new FabricaDaApi();
        await api.LerEAvancarAsync("esp32-001", 28, 50);
        await api.LerEAvancarAsync("esp32-001", 29, 50);
        await api.LerEAvancarAsync("esp32-001", 31, 50);
        var acs = await api.AcsAsync();
        var (avo, _) = await IdsDasCasasAsync(acs);

        var json = await LerJsonAsync(await acs.GetAsync($"/api/v1/domicilios/{avo}"));

        Assert.Equal("subindo", json.GetProperty("tendencia").GetString());
        Assert.Equal(3, json.GetProperty("ultimasLeituras").GetArrayLength());
        Assert.Equal(31, json.GetProperty("ultimasLeituras")[0].GetProperty("temperaturaC").GetDouble());
    }

    [Fact]
    public async Task Leituras_FiltraPorPeriodo()
    {
        await using var api = new FabricaDaApi();
        for (var i = 0; i < 4; i++)
        {
            await api.LerEAvancarAsync("esp32-001", 25 + i, 50);
        }
        var acs = await api.AcsAsync();
        var (avo, _) = await IdsDasCasasAsync(acs);
        var inicio = new DateTimeOffset(2026, 11, 16, 15, 2, 0, TimeSpan.Zero);

        var json = await LerJsonAsync(await acs.GetAsync(
            $"/api/v1/domicilios/{avo}/leituras?de={Uri.EscapeDataString(inicio.ToString("O"))}"));

        Assert.Equal(2, json.GetArrayLength()); // só as leituras das 15:02 e 15:03
    }

    [Fact]
    public async Task Familiar_VeSoAPropriaCasa_ComMensagemSimples()
    {
        await using var api = new FabricaDaApi();
        await api.LerEAvancarAsync("esp32-001", 25, 50);
        var acs = await api.AcsAsync();
        var (avo, outra) = await IdsDasCasasAsync(acs);
        var familiar = await api.FamiliarAsync();

        var propria = await LerJsonAsync(await familiar.GetAsync("/api/v1/familiar/casa"));
        var detalheProprio = await familiar.GetAsync($"/api/v1/domicilios/{avo}");
        var detalheDeOutra = await familiar.GetAsync($"/api/v1/domicilios/{outra}");
        var rotaDeAcs = await acs.GetAsync("/api/v1/familiar/casa");

        Assert.Equal("verde", propria.GetProperty("nivel").GetString());
        Assert.Contains("confortável", propria.GetProperty("mensagem").GetString());
        Assert.Equal(HttpStatusCode.OK, detalheProprio.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, detalheDeOutra.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rotaDeAcs.StatusCode);
    }

    [Fact]
    public async Task Detalhe_CasaInexistente_Da404()
    {
        await using var api = new FabricaDaApi();
        var acs = await api.AcsAsync();

        var resposta = await acs.GetAsync($"/api/v1/domicilios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}

public class CadastroTests
{
    private static object Pedido(string cpf = "529.982.247-25", bool consentimento = true, string nome = "Rosa Prado") => new
    {
        nome,
        cpf,
        dataNascimento = "1945-03-10",
        moraSozinho = true,
        temDoencaCronica = false,
        endereco = "Rua Nova, 1",
        cep = "01000-000",
        tipoCobertura = "laje",
        consentimentoConcedido = consentimento,
    };

    [Fact]
    public async Task Cadastro_SemConsentimento_Da400()
    {
        await using var api = new FabricaDaApi();
        var acs = await api.AcsAsync();

        var resposta = await acs.PostAsJsonAsync("/api/v1/residentes", Pedido("390.533.447-05", consentimento: false));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Cadastro_CpfInvalido_Da400()
    {
        await using var api = new FabricaDaApi();
        var acs = await api.AcsAsync();

        var resposta = await acs.PostAsJsonAsync("/api/v1/residentes", Pedido("111.111.111-11"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Cadastro_CpfJaExistente_Da409()
    {
        await using var api = new FabricaDaApi();
        var acs = await api.AcsAsync();

        var resposta = await acs.PostAsJsonAsync("/api/v1/residentes", Pedido("529.982.247-25"));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Cadastro_Valido_Da201_CpfMascarado_ECasaAparecenaFila()
    {
        await using var api = new FabricaDaApi();
        var acs = await api.AcsAsync();

        var resposta = await acs.PostAsJsonAsync("/api/v1/residentes", Pedido("123.456.789-09"));
        var texto = await resposta.Content.ReadAsStringAsync();
        var fila = await LerJsonAsync(await acs.GetAsync("/api/v1/visitas/fila"));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Contains("***.456.789-**", texto);
        Assert.DoesNotContain("12345678909", texto);
        Assert.Equal(6, fila.GetProperty("casas").GetArrayLength());
    }

    [Fact]
    public async Task Cadastro_Familiar_NaoPode()
    {
        await using var api = new FabricaDaApi();
        var familiar = await api.FamiliarAsync();

        var resposta = await familiar.PostAsJsonAsync("/api/v1/residentes", Pedido("123.456.789-09"));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Consentimento_ParaResidenteInexistente_Da404()
    {
        await using var api = new FabricaDaApi();
        var admin = await api.AdminAsync();

        var resposta = await admin.PostAsJsonAsync("/api/v1/consentimentos",
            new { residenteId = Guid.NewGuid(), finalidade = "Compartilhar com a UBS" });

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}

public class VisitasTests
{
    private static async Task<(Guid Casa, JsonElement Fila)> PrimeiraCasaAsync(HttpClient acs)
    {
        var fila = await LerJsonAsync(await acs.GetAsync("/api/v1/visitas/fila"));
        return (fila.GetProperty("casas")[0].GetProperty("domicilioId").GetGuid(), fila);
    }

    [Fact]
    public async Task Realizar_RegistraAVisita_ERepetirONaoDuplica()
    {
        await using var api = new FabricaDaApi();
        var acs = await api.AcsAsync();
        var (casa, _) = await PrimeiraCasaAsync(acs);
        var visitaId = Guid.NewGuid();
        var corpo = new { domicilioId = casa, resultado = "orientado", observacao = "Tomou água" };

        var primeira = await acs.PostAsJsonAsync($"/api/v1/visitas/{visitaId}/realizar", corpo);
        var repetida = await acs.PostAsJsonAsync($"/api/v1/visitas/{visitaId}/realizar", corpo);

        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        await using var escopo = api.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<TermometroDbContext>();
        Assert.Equal(1, banco.Visitas.Count());
    }

    [Fact]
    public async Task Realizar_AposAVisita_ACasaVaiParaOFimDaFilaDoSeuNivel()
    {
        await using var api = new FabricaDaApi();
        foreach (var d in new[] { "esp32-001", "sim-002", "sim-003", "sim-004", "sim-005" })
        {
            await api.NivelAposLeituraAsync(d, 25, 50); // todas verdes e iguais, só a vulnerabilidade desempata
        }
        var acs = await api.AcsAsync();
        var (primeira, _) = await PrimeiraCasaAsync(acs);

        await acs.PostAsJsonAsync($"/api/v1/visitas/{Guid.NewGuid()}/realizar",
            new { domicilioId = primeira, resultado = "tudoBem" });
        var fila = await LerJsonAsync(await acs.GetAsync("/api/v1/visitas/fila"));
        var linhaDaCasa = fila.GetProperty("casas").EnumerateArray()
            .First(c => c.GetProperty("domicilioId").GetGuid() == primeira);

        Assert.NotEqual(JsonValueKind.Null, linhaDaCasa.GetProperty("ultimaVisitaEm").ValueKind);
    }

    [Fact]
    public async Task Sincronizar_LoteOffline_EhIdempotente()
    {
        await using var api = new FabricaDaApi();
        var acs = await api.AcsAsync();
        var (casa, _) = await PrimeiraCasaAsync(acs);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var lote = new
        {
            visitas = new[]
            {
                new { id = a, domicilioId = casa, planejadaEm = (DateTimeOffset?)null, realizadaEm = api.Relogio.GetUtcNow().AddHours(-2), resultado = "tudoBem", observacao = (string?)null },
                new { id = b, domicilioId = casa, planejadaEm = (DateTimeOffset?)null, realizadaEm = api.Relogio.GetUtcNow().AddHours(-1), resultado = "orientado", observacao = "Sem ventilador" },
            },
        };

        var primeira = await LerJsonAsync(await acs.PostAsJsonAsync("/api/v1/visitas/sincronizar", lote));
        var segunda = await LerJsonAsync(await acs.PostAsJsonAsync("/api/v1/visitas/sincronizar", lote));

        Assert.All(primeira.GetProperty("visitas").EnumerateArray(),
            v => Assert.Equal("criada", v.GetProperty("status").GetString()));
        Assert.All(segunda.GetProperty("visitas").EnumerateArray(),
            v => Assert.Equal("jaExistia", v.GetProperty("status").GetString()));
        await using var escopo = api.Services.CreateAsyncScope();
        Assert.Equal(2, escopo.ServiceProvider.GetRequiredService<TermometroDbContext>().Visitas.Count());
    }

    [Fact]
    public async Task Sincronizar_ItemRuimNaoDerrubaOLote()
    {
        await using var api = new FabricaDaApi();
        var acs = await api.AcsAsync();
        var (casa, _) = await PrimeiraCasaAsync(acs);
        var boa = Guid.NewGuid();
        var noFuturo = Guid.NewGuid();
        var casaAlheia = Guid.NewGuid();
        var agora = api.Relogio.GetUtcNow();
        var lote = new
        {
            visitas = new object[]
            {
                new { id = boa, domicilioId = casa, realizadaEm = agora.AddHours(-1), resultado = "tudoBem" },
                new { id = noFuturo, domicilioId = casa, realizadaEm = agora.AddDays(2), resultado = "tudoBem" },
                new { id = casaAlheia, domicilioId = Guid.NewGuid(), realizadaEm = agora.AddHours(-1), resultado = "tudoBem" },
            },
        };

        var json = await LerJsonAsync(await acs.PostAsJsonAsync("/api/v1/visitas/sincronizar", lote));
        var status = json.GetProperty("visitas").EnumerateArray().Select(v => v.GetProperty("status").GetString()).ToArray();

        Assert.Equal(["criada", "rejeitada", "rejeitada"], status);
    }

    [Fact]
    public async Task Sincronizar_LoteGrandeDemais_Da400()
    {
        await using var api = new FabricaDaApi();
        var acs = await api.AcsAsync();
        var visitas = Enumerable.Range(0, 201).Select(_ => new
        {
            id = Guid.NewGuid(), domicilioId = Guid.NewGuid(), realizadaEm = api.Relogio.GetUtcNow(), resultado = "tudoBem",
        });

        var resposta = await acs.PostAsJsonAsync("/api/v1/visitas/sincronizar", new { visitas });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}

public class IsolamentoEntreMicroareasTests
{
    /// <summary>Cria uma segunda microárea com uma casa, que a ACS Ana NÃO deve enxergar.</summary>
    private static async Task<Guid> CriarCasaDeOutraMicroareaAsync(FabricaDaApi api)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<TermometroDbContext>();
        var outra = new Microarea("Microárea 02");
        var residente = new Residente("Vizinho de Outra Área", Cpf.Criar("123.456.789-09"), new DateOnly(1940, 1, 1), true, true);
        var casa = new Domicilio(residente.Id, outra.Id, "Rua Distante, 99", "02000-000", TipoCobertura.Laje);
        banco.AddRange(outra, residente, casa);
        await banco.SaveChangesAsync();
        return casa.Id;
    }

    [Fact]
    public async Task Acs_NaoVeCasaDeOutraMicroarea_NemNaFilaNemNoDetalhe()
    {
        await using var api = new FabricaDaApi();
        var casaAlheia = await CriarCasaDeOutraMicroareaAsync(api);
        var acs = await api.AcsAsync();

        var fila = await LerJsonAsync(await acs.GetAsync("/api/v1/visitas/fila"));
        var detalhe = await acs.GetAsync($"/api/v1/domicilios/{casaAlheia}");
        var leituras = await acs.GetAsync($"/api/v1/domicilios/{casaAlheia}/leituras");

        Assert.Equal(5, fila.GetProperty("casas").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, detalhe.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, leituras.StatusCode);
    }

    [Fact]
    public async Task Acs_NaoRegistraNemSincronizaVisitaEmCasaDeOutraMicroarea()
    {
        await using var api = new FabricaDaApi();
        var casaAlheia = await CriarCasaDeOutraMicroareaAsync(api);
        var acs = await api.AcsAsync();
        var agora = api.Relogio.GetUtcNow();

        var online = await acs.PostAsJsonAsync($"/api/v1/visitas/{Guid.NewGuid()}/realizar",
            new { domicilioId = casaAlheia, resultado = "tudoBem" });
        var offline = await LerJsonAsync(await acs.PostAsJsonAsync("/api/v1/visitas/sincronizar", new
        {
            visitas = new[] { new { id = Guid.NewGuid(), domicilioId = casaAlheia, realizadaEm = agora.AddHours(-1), resultado = "tudoBem" } },
        }));

        Assert.Equal(HttpStatusCode.Forbidden, online.StatusCode);
        Assert.Equal("rejeitada", offline.GetProperty("visitas")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Admin_VeQualquerCasa()
    {
        await using var api = new FabricaDaApi();
        var casaAlheia = await CriarCasaDeOutraMicroareaAsync(api);
        var admin = await api.AdminAsync();

        var detalhe = await admin.GetAsync($"/api/v1/domicilios/{casaAlheia}");

        Assert.Equal(HttpStatusCode.OK, detalhe.StatusCode);
    }
}
