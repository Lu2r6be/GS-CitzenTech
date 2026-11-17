using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Termometro.Api.Autenticacao;
using Termometro.Application.Visitas;

namespace Termometro.Api.Controllers;

public sealed record LoteDeVisitas(IReadOnlyList<VisitaOffline> Visitas);

[ApiController]
[Route("api/v1/visitas")]
[Authorize(Roles = "Acs")]
public sealed class VisitasController(ObterFila fila, SincronizarVisitas sincronizar, RealizarVisita realizar)
    : ControllerBase
{
    /// <summary>A fila de visitas da microárea do ACS, da casa mais urgente para a menos.</summary>
    [HttpGet("fila")]
    public async Task<ActionResult<FilaDeVisitas>> Fila(CancellationToken ct) =>
        Ok(await fila.ExecutarAsync(User.ParaUsuarioAutenticado(), ct));

    /// <summary>Recebe as visitas feitas offline. Mandar o mesmo lote duas vezes não duplica nada.</summary>
    [HttpPost("sincronizar")]
    public async Task<ActionResult<ResultadoDaSincronizacao>> Sincronizar(LoteDeVisitas lote, CancellationToken ct) =>
        Ok(await sincronizar.ExecutarAsync(User.ParaUsuarioAutenticado(), lote.Visitas ?? [], ct));

    /// <summary>Registra uma visita feita com internet. O id (GUID) é gerado pelo app.</summary>
    [HttpPost("{id:guid}/realizar")]
    public async Task<ActionResult<VisitaRegistrada>> Realizar(Guid id, RealizarVisitaPedido pedido, CancellationToken ct) =>
        Ok(await realizar.ExecutarAsync(User.ParaUsuarioAutenticado(), id, pedido, ct));
}
