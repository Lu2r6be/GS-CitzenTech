using Microsoft.Extensions.Logging;
using Termometro.Domain.Entidades;
using Termometro.Domain.Seguranca;

namespace Termometro.Application.Seguranca;

/// <summary>
/// Tenta o LLM primeiro; se falhar, estourar o tempo ou devolver algo que contenha dado sensível,
/// usa o texto-modelo. Guarda a resposta por alguns minutos para o painel não chamar a IA
/// (e gastar crédito) a cada atualização.
///
/// O prompt é o "sumidouro" PromptLlm do taint analysis: antes de sair, passa pelo detector. Se achar
/// dado pessoal ali, NÃO envia e usa a reserva.
/// </summary>
public sealed class TradutorComReserva(
    ITradutorDeAlerta principal,
    TimeProvider relogio,
    ILogger<TradutorComReserva> log) : ITradutorDeAlerta
{
    public static readonly TimeSpan ValidadeDoCache = TimeSpan.FromMinutes(10);

    private readonly DetectorDePii _detector = new();
    private readonly object _trava = new();
    private (string Chave, DateTimeOffset Ate, AlertaAoCidadao Alerta)? _cache;

    public async Task<AlertaAoCidadao> TraduzirAsync(ResumoParaAlerta resumo, CancellationToken ct)
    {
        var chave = ChaveDoResumo(resumo);
        var agora = relogio.GetUtcNow();
        lock (_trava)
        {
            if (_cache is { } c && c.Chave == chave && c.Ate > agora)
            {
                return c.Alerta;
            }
        }

        AlertaAoCidadao alerta;
        try
        {
            var bruto = await principal.TraduzirAsync(resumo, ct);
            alerta = Conferir(bruto) ?? throw new InvalidOperationException("O alerta do LLM continha dado sensível ou veio vazio.");
        }
        catch (Exception erro) when (erro is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Só o TIPO do erro vai para o log: a mensagem pode citar trechos do que foi enviado.
            log.LogWarning("Tradução por IA indisponível ({TipoDoErro}); usando o texto-modelo.", erro.GetType().Name);
            alerta = TradutorDeReserva.Montar(resumo);
        }

        lock (_trava)
        {
            _cache = (chave, agora + ValidadeDoCache, alerta);
        }
        return alerta;
    }

    /// <summary>A resposta da IA também é uma saída: nada de dado pessoal inventado ou repetido.</summary>
    private AlertaAoCidadao? Conferir(AlertaAoCidadao a)
    {
        if (string.IsNullOrWhiteSpace(a.Titulo) || string.IsNullOrWhiteSpace(a.Mensagem))
        {
            return null;
        }
        var tudo = a.Titulo + "\n" + a.Mensagem + "\n" + string.Join("\n", a.Acoes);
        return _detector.ContemPii(tudo) ? null : a;
    }

    private static string ChaveDoResumo(ResumoParaAlerta r) =>
        $"{r.Pontuacao}|{r.TotalDeAchados}|{r.AbertosNoCodigo}|{r.BarradosEmExecucao}|"
        + string.Join(",", r.PorCategoria.Select(g => g.Grupo + g.Quantidade))
        + "|" + string.Join(",", r.PorSaida.Select(g => g.Grupo + g.Quantidade));
}
