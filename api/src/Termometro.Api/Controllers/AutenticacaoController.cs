using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Termometro.Application.Autenticacao;

namespace Termometro.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AutenticacaoController(Autenticar autenticar) : ControllerBase
{
    /// <summary>Entra com e-mail e senha e recebe o token (JWT) para as demais chamadas.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResposta>> Login(LoginPedido pedido, CancellationToken ct) =>
        Ok(await autenticar.ExecutarAsync(pedido, ct));
}
