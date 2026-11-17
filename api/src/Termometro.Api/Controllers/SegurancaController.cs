using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Termometro.Api.Autenticacao;
using Termometro.Application.Erros;
using Termometro.Application.Seguranca;

namespace Termometro.Api.Controllers;

/// <summary>Módulo de segurança: indicador Risco Cidadão, importação de achados e análise de PII. Só administrador.</summary>
[ApiController]
[Route("api/v1/seguranca")]
[Authorize(Roles = "Admin")]
public sealed class SegurancaController(
    ConsultarRiscoCidadao riscoCidadao,
    ImportarAchados importar,
    AnalisarTexto analisar,
    IConfiguration configuracao,
    ILogger<SegurancaController> log) : ControllerBase
{
    /// <summary>O indicador Risco Cidadão (0 a 100), os achados recentes e o alerta em linguagem simples.</summary>
    [HttpGet("risco-cidadao")]
    public async Task<ActionResult<PainelRiscoCidadao>> RiscoCidadao(CancellationToken ct) =>
        Ok(await riscoCidadao.ExecutarAsync(User.ParaUsuarioAutenticado(), ct));

    /// <summary>Recebe achados de ferramentas (Semgrep em modo taint, Trufflehog). Veja security/ferramentas/importar_achados.py.</summary>
    [HttpPost("achados")]
    public async Task<ActionResult<ImportacaoConcluida>> Importar(ImportarAchadosPedido pedido, CancellationToken ct) =>
        Ok(await importar.ExecutarAsync(User.ParaUsuarioAutenticado(), pedido, ct));

    /// <summary>Roda o motor de PII num texto: devolve onde achou cada tipo de dado e o texto já sanitizado. Não devolve os valores achados.</summary>
    [HttpPost("analisar-texto")]
    public ActionResult<TextoAnalisado> AnalisarTexto(AnalisarTextoPedido pedido) =>
        Ok(analisar.Executar(User.ParaUsuarioAutenticado(), pedido));

    /// <summary>
    /// Só na demonstração: recebe um texto de teste (com dados FICTÍCIOS), escreve no log e devolve na resposta,
    /// como um programador descuidado faria. Os guardas de log e de resposta barram o vazamento e o painel registra.
    /// </summary>
    [HttpPost("simular-vazamento")]
    public ActionResult<object> SimularVazamento(SimularVazamentoPedido pedido)
    {
        if (!configuracao.GetValue("Demonstracao:Habilitada", false))
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(pedido.Texto) || pedido.Texto.Length > AnalisarTexto_TamanhoMaximo)
        {
            throw new DadosInvalidos("Envie um texto de teste de 1 a 2000 caracteres.");
        }

        log.LogInformation("Texto de teste recebido: {Texto}", pedido.Texto);
        return Ok(new { eco = pedido.Texto });
    }

    private const int AnalisarTexto_TamanhoMaximo = 2000;
}

public sealed record SimularVazamentoPedido(string Texto);
