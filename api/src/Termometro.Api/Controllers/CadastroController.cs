using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Termometro.Api.Autenticacao;
using Termometro.Application.Cadastro;

namespace Termometro.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "Acs,Admin")]
public sealed class CadastroController(CadastrarResidente cadastrar, RegistrarConsentimento consentir)
    : ControllerBase
{
    /// <summary>Cadastra residente + casa + consentimento. Sem consentimento, não cadastra. O CPF volta mascarado.</summary>
    [HttpPost("residentes")]
    public async Task<ActionResult<ResidenteCadastrado>> Cadastrar(CadastrarResidentePedido pedido, CancellationToken ct)
    {
        var criado = await cadastrar.ExecutarAsync(User.ParaUsuarioAutenticado(), pedido, ct);
        return Created($"/api/v1/domicilios/{criado.DomicilioId}", criado);
    }

    /// <summary>Registra um novo consentimento de um residente já cadastrado.</summary>
    [HttpPost("consentimentos")]
    public async Task<ActionResult<ConsentimentoRegistrado>> Consentir(RegistrarConsentimentoPedido pedido, CancellationToken ct)
    {
        var criado = await consentir.ExecutarAsync(User.ParaUsuarioAutenticado(), pedido, ct);
        return Created(string.Empty, criado);
    }
}
