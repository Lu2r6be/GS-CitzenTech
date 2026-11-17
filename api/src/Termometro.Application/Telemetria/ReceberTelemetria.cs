using Termometro.Application.Abstracoes;
using Termometro.Application.Erros;
using Termometro.Application.Risco;
using Termometro.Domain.Calor;
using Termometro.Domain.Entidades;
using Termometro.Domain.Niveis;

namespace Termometro.Application.Telemetria;

public sealed record TelemetriaRecebida(string DeviceId, double TemperaturaC, double Umidade);

/// <summary>
/// Caminho do dado: ESP32 → valida → classifica → confirma (histerese) → grava.
/// O ESP32 manda só dado bruto; quem decide a cor é aqui.
/// </summary>
public sealed class ReceberTelemetria(
    IMonitoramentoRepositorio monitoramento,
    IUnidadeDeTrabalho unidade,
    IHashDeChaveDeDispositivo hashDeChave,
    IClassificadorNivel classificador,
    ServicoDiaDeRisco diaDeRisco)
{
    // Limites físicos do DHT22 (-40 a 80 °C). Fora disso o sensor está com defeito: melhor recusar.
    private const double TemperaturaMinimaC = -40;
    private const double TemperaturaMaximaC = 80;

    public async Task<NivelTermico> ExecutarAsync(TelemetriaRecebida telemetria, string? chaveDoDispositivo,
        CancellationToken ct)
    {
        Validar(telemetria);

        var dispositivo = await monitoramento.ObterDispositivoAsync(telemetria.DeviceId.Trim(), ct);
        // Mesma mensagem para "não existe" e "chave errada": não ensina a descobrir ids válidos.
        if (dispositivo is not { Ativo: true } || string.IsNullOrEmpty(chaveDoDispositivo)
            || !hashDeChave.Verificar(chaveDoDispositivo, dispositivo.DeviceKeyHash))
        {
            throw new NaoAutenticado("Dispositivo não autorizado.");
        }

        var casa = await monitoramento.ObterDomicilioAsync(dispositivo.DomicilioId, ct)
            ?? throw new NaoEncontrado("O dispositivo não está ligado a nenhuma casa.");

        var agora = diaDeRisco.Agora;
        var limiares = await monitoramento.ObterLimiarAtualAsync(ct) ?? LimiarTemperatura.Padrao;
        var dia = await diaDeRisco.DeHojeAsync(ct);

        var nivelLido = classificador.Classificar(telemetria.TemperaturaC, telemetria.Umidade, limiares, dia);
        var nivelConfirmado = casa.RegistrarLeitura(nivelLido, agora);

        // A leitura guarda o que o sensor mediu; a casa guarda o nível confirmado.
        await monitoramento.AdicionarLeituraAsync(new Leitura(
            dispositivo.Id, agora, telemetria.TemperaturaC, telemetria.Umidade,
            IndiceCalor.Calcular(telemetria.TemperaturaC, telemetria.Umidade), nivelLido), ct);
        await unidade.SalvarAsync(ct);

        return nivelConfirmado;
    }

    private static void Validar(TelemetriaRecebida t)
    {
        if (string.IsNullOrWhiteSpace(t.DeviceId))
        {
            throw new DadosInvalidos("deviceId é obrigatório.");
        }
        if (double.IsNaN(t.TemperaturaC) || t.TemperaturaC is < TemperaturaMinimaC or > TemperaturaMaximaC)
        {
            throw new DadosInvalidos($"temperaturaC deve estar entre {TemperaturaMinimaC} e {TemperaturaMaximaC}.");
        }
        if (double.IsNaN(t.Umidade) || t.Umidade is < 0 or > 100)
        {
            throw new DadosInvalidos("umidade deve estar entre 0 e 100.");
        }
    }
}
