using Termometro.Application.Abstracoes;
using Termometro.Domain.Niveis;

namespace Termometro.Application.Config;

public sealed record LimiaresAtuais(double AzulAbaixoDe, double VerdeAte, double AmareloAte, string Versao, string Origem);

/// <summary>Os limiares em vigor. O ESP32 lê na inicialização (só para mostrar; quem decide a cor é a API).</summary>
public sealed class ConsultarLimiares(IMonitoramentoRepositorio monitoramento)
{
    public async Task<LimiaresAtuais> ExecutarAsync(CancellationToken ct)
    {
        var l = await monitoramento.ObterLimiarAtualAsync(ct) ?? LimiarTemperatura.Padrao;
        return new LimiaresAtuais(l.AzulAbaixoDe, l.VerdeAte, l.AmareloAte, l.Versao, l.Origem.ToString());
    }
}
