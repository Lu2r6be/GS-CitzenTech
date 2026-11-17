using System.Collections.Concurrent;
using Termometro.Application.Seguranca;
using Termometro.Domain.Entidades;
using Termometro.Domain.Seguranca;

namespace Termometro.Api.Seguranca;

/// <summary>
/// O "guarda" da saída LOG. Todo texto de log passa pelo sanitizador ANTES de chegar ao console.
/// Se havia dado sensível, o log sai limpo e um achado "runtime:log-guard" é registrado
/// (a falha de quem escreveu o log continua visível no painel Risco Cidadão).
///
/// É a segunda camada: o Semgrep acha o problema no CÓDIGO; este guarda segura o que passar.
/// </summary>
public sealed class ProviderDeLogSeguro(IServiceProvider servicos) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, LoggerSeguro> _loggers = new();

    public ILogger CreateLogger(string categoria) =>
        _loggers.GetOrAdd(categoria, c => new LoggerSeguro(c, servicos));

    public void Dispose() => _loggers.Clear();
}

internal sealed class LoggerSeguro(string categoria, IServiceProvider servicos) : ILogger
{
    private static readonly PiiSanitizer Sanitizador = new();
    private static readonly object Trava = new();

    // Evita laço: gravar o achado pode gerar log, que passaria pelo guarda de novo.
    [ThreadStatic] private static bool _registrando;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel nivel) => nivel >= LogLevel.Information;

    public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? erro,
        Func<TState, Exception?, string> formatador)
    {
        if (!IsEnabled(nivel))
        {
            return;
        }

        var mensagem = formatador(estado, null);
        var limpa = Sanitizador.Sanitizar(mensagem, ExposicaoAchado.Log);
        var texto = limpa.Texto;
        var categorias = limpa.Categorias.ToHashSet();

        if (erro is not null)
        {
            // O texto de uma exceção também pode trazer dado (ex.: "CPF 123... inválido"). Limpa igual.
            var erroLimpo = Sanitizador.Sanitizar(erro.ToString(), ExposicaoAchado.Log);
            texto += Environment.NewLine + erroLimpo.Texto;
            categorias.UnionWith(erroLimpo.Categorias);
        }

        if (categorias.Count > 0 && !_registrando)
        {
            _registrando = true;
            try
            {
                var registrador = servicos.GetService<IRegistradorDeAchados>();
                foreach (var c in categorias)
                {
                    registrador?.Registrar(new NovoAchado($"pii-{c.ToString().ToLowerInvariant()}-em-log",
                        CalculadorDoRiscoCidadao.SeveridadePadrao(c, ExposicaoAchado.Log), "runtime:log-guard",
                        ExposicaoAchado.Log, c));
                }
            }
            finally
            {
                _registrando = false;
            }
        }

        var linha = $"{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz} {Sigla(nivel)} {categoria}: {texto}";
        lock (Trava)
        {
            Console.Out.WriteLine(linha);
        }
    }

    private static string Sigla(LogLevel n) => n switch
    {
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERRO",
        LogLevel.Critical => "CRIT",
        _ => n.ToString().ToUpperInvariant(),
    };
}
