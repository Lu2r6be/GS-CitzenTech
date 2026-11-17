using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Termometro.Api.Autenticacao;
using Termometro.Application.Domicilios;

namespace Termometro.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class CasasController(ConsultarCasas casas) : ControllerBase
{
    /// <summary>Resumo da casa e últimas leituras (ACS da microárea, admin ou familiar da casa).</summary>
    [HttpGet("domicilios/{id:guid}")]
    public async Task<ActionResult<DetalheDaCasa>> Detalhar(Guid id, CancellationToken ct) =>
        Ok(await casas.DetalharAsync(User.ParaUsuarioAutenticado(), id, ct));

    /// <summary>Histórico de leituras, opcionalmente entre duas datas.</summary>
    [HttpGet("domicilios/{id:guid}/leituras")]
    public async Task<ActionResult<IReadOnlyList<LeituraResumo>>> Leituras(Guid id,
        [FromQuery] DateTimeOffset? de, [FromQuery] DateTimeOffset? ate, CancellationToken ct) =>
        Ok(await casas.LeiturasAsync(User.ParaUsuarioAutenticado(), id, de, ate, ct));

    /// <summary>Status simples da casa vinculada ao familiar.</summary>
    [HttpGet("familiar/casa")]
    [Authorize(Roles = "Familiar")]
    public async Task<ActionResult<CasaDoFamiliar>> DoFamiliar(CancellationToken ct) =>
        Ok(await casas.DoFamiliarAsync(User.ParaUsuarioAutenticado(), ct));
}
