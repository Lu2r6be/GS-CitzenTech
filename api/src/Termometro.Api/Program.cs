using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Termometro.Api.Autenticacao;
using Termometro.Api.Demonstracao;
using Termometro.Api.Erros;
using Termometro.Application;
using Termometro.Api.Seguranca;
using Termometro.Application.Abstracoes;
using Termometro.Application.Seguranca;
using Termometro.Infrastructure;
using Termometro.Infrastructure.Persistencia;

var builder = WebApplication.CreateBuilder(args);
var configuracao = builder.Configuration;

// ---------- Banco: PostgreSQL na VM, SQLite no PC de desenvolvimento ----------
var usaSqlite = string.Equals(configuracao["Banco:Provedor"], "Sqlite", StringComparison.OrdinalIgnoreCase);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(opcoes =>
{
    if (usaSqlite)
    {
        opcoes.UseSqlite(configuracao["Banco:ConexaoSqlite"]);
    }
    else
    {
        var conexao = configuracao["Banco:ConexaoPostgres"];
        if (string.IsNullOrWhiteSpace(conexao))
        {
            throw new InvalidOperationException("Defina Banco:ConexaoPostgres (variável Banco__ConexaoPostgres).");
        }
        opcoes.UseNpgsql(conexao);
    }
});

// ---------- JWT ----------
var opcoesJwt = configuracao.GetSection(OpcoesJwt.Secao).Get<OpcoesJwt>() ?? new OpcoesJwt();
if (Encoding.UTF8.GetByteCount(opcoesJwt.Chave) < OpcoesJwt.TamanhoMinimoDaChave)
{
    // A chave nunca fica no repositório: vem de variável de ambiente (Jwt__Chave).
    throw new InvalidOperationException(
        $"Jwt:Chave precisa ter ao menos {OpcoesJwt.TamanhoMinimoDaChave} caracteres (variável Jwt__Chave).");
}
builder.Services.Configure<OpcoesJwt>(configuracao.GetSection(OpcoesJwt.Secao));
builder.Services.AddSingleton<IEmissorDeToken, EmissorDeTokenJwt>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        opcoes.MapInboundClaims = false; // mantém "sub" e "role" com os nomes curtos que o emissor usa
        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = opcoesJwt.Emissor,
            ValidAudience = opcoesJwt.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcoesJwt.Chave)),
            NameClaimType = Claims.Id,
            RoleClaimType = Claims.Papel,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
// A validade do token é conferida pelo MESMO relógio da aplicação (e dos testes), não por um escondido.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TimeProvider>((jwt, relogio) =>
        jwt.TokenValidationParameters.LifetimeValidator = (inicio, fim, _, parametros) =>
        {
            var agora = relogio.GetUtcNow().UtcDateTime;
            return (inicio is null || inicio.Value - parametros.ClockSkew <= agora)
                && (fim is not null && agora <= fim.Value + parametros.ClockSkew);
        });
builder.Services.AddAuthorization();

// ---------- Limite de tentativas de login (força bruta) ----------
var tentativasPorMinuto = configuracao.GetValue("LimiteDeTentativas:LoginPorMinuto", 10);
builder.Services.AddRateLimiter(limite =>
{
    limite.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limite.AddPolicy("login", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = tentativasPorMinuto, Window = TimeSpan.FromMinutes(1) }));
});

// ---------- Web ----------
builder.Services.AddControllers().AddJsonOptions(json =>
{
    // "azul", "vermelho", "tudoBem"...: texto legível em vez de números.
    json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddExceptionHandler<TradutorDeErros>();

// ---------- Módulo de segurança (PII, achados, Risco Cidadão) ----------
// Logs passam por um guarda que sanitiza dado pessoal antes de escrever (substitui o console padrão).
builder.Logging.ClearProviders();
builder.Services.AddSingleton<ILoggerProvider>(sp => new ProviderDeLogSeguro(sp));
builder.Services.Configure<OpcoesDeSeguranca>(configuracao.GetSection(OpcoesDeSeguranca.Secao));
builder.Services.AddSingleton<RegistradorDeAchadosEmSegundoPlano>();
builder.Services.AddSingleton<IRegistradorDeAchados>(sp => sp.GetRequiredService<RegistradorDeAchadosEmSegundoPlano>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RegistradorDeAchadosEmSegundoPlano>());

// LLM (Mistral): a chave vem de Mistral__ApiKey ou MISTRAL_API_KEY, nunca do repositório. Sem chave, vale o texto-modelo.
builder.Services.Configure<OpcoesMistral>(configuracao.GetSection(OpcoesMistral.Secao));
builder.Services.PostConfigure<OpcoesMistral>(o =>
{
    if (string.IsNullOrWhiteSpace(o.ApiKey))
    {
        o.ApiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY") ?? string.Empty;
    }
});
builder.Services.AddHttpClient("mistral");
builder.Services.AddSingleton<ITradutorDeAlerta>(sp => new TradutorComReserva(
    new TradutorMistral(sp.GetRequiredService<IHttpClientFactory>().CreateClient("mistral"),
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpcoesMistral>>()),
    sp.GetRequiredService<TimeProvider>(),
    sp.GetRequiredService<ILogger<TradutorComReserva>>()));
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<TermometroDbContext>("banco");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger =>
{
    swagger.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Termômetro do ACS",
        Version = "v1",
        Description = "Semáforo térmico de casas de idosos e fila de visitas dos agentes comunitários de saúde.",
    });
    swagger.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Faça login em /api/v1/auth/login e cole aqui o token.",
    });
    swagger.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", documento)] = [],
    });
});

var app = builder.Build();

// ---------- Banco pronto e dados de demonstração ----------
await using (var escopo = app.Services.CreateAsyncScope())
{
    var banco = escopo.ServiceProvider.GetRequiredService<TermometroDbContext>();
    if (usaSqlite)
    {
        await banco.Database.EnsureCreatedAsync(); // SQLite local: cria as tabelas direto do modelo
    }
    else if (configuracao.GetValue("Banco:AplicarMigrations", true))
    {
        await banco.Database.MigrateAsync(); // PostgreSQL: aplica as migrations
    }

    var demonstracao = configuracao.GetSection(OpcoesDeDemonstracao.Secao).Get<OpcoesDeDemonstracao>();
    if (demonstracao is { Habilitada: true })
    {
        await ActivatorUtilities.CreateInstance<SemeadorDeDemonstracao>(escopo.ServiceProvider)
            .SemearAsync(demonstracao, CancellationToken.None);
    }
}

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();                                // painel Risco Cidadão em /painel/
app.UseMiddleware<ProtecaoDeRespostaMiddleware>();   // guarda: nenhuma resposta da API sai com CPF inteiro ou senha
app.MapGet("/painel", () => Results.Redirect("/painel/index.html"));
app.UseSwagger();
app.UseSwaggerUI();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Torna o Program visível para os testes de integração.</summary>
public partial class Program;
