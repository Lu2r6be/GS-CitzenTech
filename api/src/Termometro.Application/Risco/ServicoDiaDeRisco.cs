using Termometro.Application.Abstracoes;
using Termometro.Domain.Niveis;

namespace Termometro.Application.Risco;

/// <summary>
/// Diz se HOJE é dia de calor, de frio ou normal lá fora.
/// 1º a tabela DiaRisco (saída do ML); se o dia não está lá, calcula pelo clima externo mais recente.
/// </summary>
public sealed class ServicoDiaDeRisco(IMonitoramentoRepositorio monitoramento, TimeProvider relogio)
{
    // O INMET e o ML contam o dia no horário de Brasília (sem horário de verão desde 2019).
    private static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);
    private static readonly TimeSpan IdadeMaximaDoClima = TimeSpan.FromHours(36);

    public DateTimeOffset Agora => relogio.GetUtcNow();

    public async Task<TipoDiaRisco> DeHojeAsync(CancellationToken ct)
    {
        var agora = Agora;
        var hoje = DateOnly.FromDateTime(agora.ToOffset(Brasilia).DateTime);

        var diaPrevisto = await monitoramento.ObterDiaRiscoAsync(hoje, ct);
        if (diaPrevisto is not null)
        {
            return diaPrevisto.Tipo;
        }

        var clima = await monitoramento.ObterClimaMaisRecenteAsync(ct);
        // Clima velho não vale: "sem informação" é melhor que um dado de ontem passando por de hoje.
        return clima is not null && agora - clima.ObservadoEm <= IdadeMaximaDoClima
            ? ClassificadorDiaDeRisco.Classificar(clima.TempMaxC, clima.TempMinC)
            : TipoDiaRisco.Nenhum;
    }
}
