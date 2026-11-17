using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Termometro.Application.Config;
using Termometro.Application.Erros;
using Termometro.Application.Telemetria;
using Termometro.Domain.Niveis;

namespace Termometro.Api.Controllers;

/// <summary>Mensagem do ESP32. Os campos de medida são opcionais aqui só para a API poder recusar quem esquece.</summary>
public sealed record TelemetriaRequisicao(string? DeviceId, double? TemperaturaC, double? Umidade);

public sealed record NivelResposta(NivelTermico Nivel);

[ApiController]
[Route("api/v1")]
public sealed class TelemetriaController(ReceberTelemetria receber, ConsultarLimiares limiares) : ControllerBase
{
    public const string CabecalhoDaChave = "X-Device-Key";

    /// <summary>O sensor envia temperatura e umidade; a API responde o nível para acender o LED.</summary>
    [HttpPost("telemetria")]
    [AllowAnonymous] // quem autentica aqui é a chave do dispositivo, não um usuário
    public async Task<IActionResult> Receber(TelemetriaRequisicao requisicao,
        [FromHeader(Name = CabecalhoDaChave)] string? chave, CancellationToken ct)
    {
        // Sem este teste, um campo esquecido virava 0 °C (e o semáforo ficaria azul "de mentira").
        if (requisicao.TemperaturaC is null || requisicao.Umidade is null)
        {
            throw new DadosInvalidos("temperaturaC e umidade são obrigatórios.");
        }

        var nivel = await receber.ExecutarAsync(
            new TelemetriaRecebida(requisicao.DeviceId ?? string.Empty, requisicao.TemperaturaC.Value,
                requisicao.Umidade.Value), chave, ct);

        return Accepted(new NivelResposta(nivel));
    }

    /// <summary>Limiares em vigor (o ESP32 lê na inicialização).</summary>
    [HttpGet("config/limiares")]
    [AllowAnonymous]
    public async Task<ActionResult<LimiaresAtuais>> Limiares(CancellationToken ct) =>
        Ok(await limiares.ExecutarAsync(ct));
}
