using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Termometro.Api.Demonstracao;

namespace Termometro.Api.Tests;

/// <summary>Relógio que só anda quando o teste manda: assim "30 minutos sem sinal" testa em milissegundos.</summary>
public sealed class RelogioDeTeste(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset _agora = inicio;
    public override DateTimeOffset GetUtcNow() => _agora;
    public void Avancar(TimeSpan tempo) => _agora += tempo;
}

/// <summary>
/// A API inteira de verdade (controllers, JWT, EF Core), com banco SQLite em arquivo temporário
/// e o cenário de demonstração (5 casas) já carregado. Uma instância nova por teste.
/// </summary>
public sealed class FabricaDaApi : WebApplicationFactory<Program>
{
    public const string Senha = "Demo@2026!";
    public const string ChaveDoDispositivo = "chave-demo-do-esp32-001";

    private readonly string _arquivoDoBanco = Path.Combine(Path.GetTempPath(), $"termometro-teste-{Guid.NewGuid():N}.db");

    public RelogioDeTeste Relogio { get; } = new(new DateTimeOffset(2026, 11, 16, 15, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Banco:Provedor", "Sqlite");
        builder.UseSetting("Banco:ConexaoSqlite", $"Data Source={_arquivoDoBanco}");
        builder.UseSetting("Jwt:Chave", "chave-de-teste-com-mais-de-32-caracteres-0123456789");
        builder.UseSetting("Demonstracao:Habilitada", "true");
        builder.UseSetting("Demonstracao:SenhaDosUsuarios", Senha);
        builder.UseSetting("Demonstracao:ChaveDoDispositivo", ChaveDoDispositivo);
        builder.UseSetting("LimiteDeTentativas:LoginPorMinuto", "1000");
        builder.UseSetting("Seguranca:JanelaDeAgrupamentoEmSegundos", "0");

        builder.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(Relogio);
        });
    }

    public async Task<HttpClient> ClienteLogadoAsync(string email)
    {
        var cliente = CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, senha = Senha });
        resposta.EnsureSuccessStatusCode();
        var login = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());
        return cliente;
    }

    public Task<HttpClient> AcsAsync() => ClienteLogadoAsync(SemeadorDeDemonstracao.EmailAcs);
    public Task<HttpClient> AdminAsync() => ClienteLogadoAsync(SemeadorDeDemonstracao.EmailAdmin);
    public Task<HttpClient> FamiliarAsync() => ClienteLogadoAsync(SemeadorDeDemonstracao.EmailFamiliar);

    /// <summary>Simula o ESP32 enviando uma leitura. Devolve a resposta crua para o teste olhar status e nível.</summary>
    public Task<HttpResponseMessage> EnviarLeituraAsync(string dispositivo, double temperatura, double umidade,
        string? chave = ChaveDoDispositivo)
    {
        var cliente = CreateClient();
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/v1/telemetria")
        {
            Content = JsonContent.Create(new { deviceId = dispositivo, temperaturaC = temperatura, umidade }),
        };
        if (chave is not null)
        {
            pedido.Headers.Add("X-Device-Key", chave);
        }
        return cliente.SendAsync(pedido);
    }

    public async Task<string> NivelAposLeituraAsync(string dispositivo, double temperatura, double umidade)
    {
        var resposta = await EnviarLeituraAsync(dispositivo, temperatura, umidade);
        Assert.Equal(System.Net.HttpStatusCode.Accepted, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("nivel").GetString()!;
    }

    /// <summary>Cada leitura é gravada com a hora do relógio; avançar 1 min evita leituras "no mesmo instante".</summary>
    public async Task<string> LerEAvancarAsync(string dispositivo, double temperatura, double umidade)
    {
        var nivel = await NivelAposLeituraAsync(dispositivo, temperatura, umidade);
        Relogio.Avancar(TimeSpan.FromMinutes(1));
        return nivel;
    }

    public static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta) =>
        await resposta.Content.ReadFromJsonAsync<JsonElement>();

    public override ValueTask DisposeAsync()
    {
        File.Delete(_arquivoDoBanco);
        return base.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_arquivoDoBanco))
        {
            try { File.Delete(_arquivoDoBanco); } catch (IOException) { /* o SQLite pode demorar a soltar o arquivo */ }
        }
    }
}
