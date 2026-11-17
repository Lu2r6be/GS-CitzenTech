using System.Text;
using Termometro.Application.Seguranca;
using Termometro.Domain.Entidades;
using Termometro.Domain.Seguranca;

namespace Termometro.Api.Seguranca;

/// <summary>
/// O "guarda" da saída RESPOSTA HTTP. Lê o corpo das respostas JSON/texto da API e aplica a política:
/// CPF inteiro e senha NUNCA saem (o CPF vira ***.456.789-**). Se algo foi trocado, registra um achado.
///
/// Só olha caminhos /api/ e corpos pequenos (até 1 MB). Saúde e e-mail podem sair: o ACS autorizado precisa deles.
/// </summary>
public sealed class ProtecaoDeRespostaMiddleware(RequestDelegate proximo, IRegistradorDeAchados registrador)
{
    private const int LimiteDeBytes = 1_000_000;
    private static readonly PiiSanitizer Sanitizador = new();

    public async Task InvokeAsync(HttpContext contexto)
    {
        if (!contexto.Request.Path.StartsWithSegments("/api"))
        {
            await proximo(contexto);
            return;
        }

        var original = contexto.Response.Body;
        await using var memoria = new MemoryStream();
        contexto.Response.Body = memoria;
        try
        {
            await proximo(contexto);
        }
        finally
        {
            contexto.Response.Body = original;
        }

        memoria.Position = 0;
        var tipo = contexto.Response.ContentType ?? string.Empty;
        var inspecionavel = (tipo.Contains("json", StringComparison.OrdinalIgnoreCase)
                             || tipo.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                            && memoria.Length is > 0 and <= LimiteDeBytes;

        if (!inspecionavel)
        {
            await memoria.CopyToAsync(original, contexto.RequestAborted);
            return;
        }

        var corpo = Encoding.UTF8.GetString(memoria.ToArray());
        var limpo = Sanitizador.Sanitizar(corpo, ExposicaoAchado.RespostaHttp);
        if (!limpo.HouveSubstituicao)
        {
            await memoria.CopyToAsync(original, contexto.RequestAborted);
            return;
        }

        foreach (var categoria in limpo.Categorias)
        {
            registrador.Registrar(new NovoAchado($"pii-{categoria.ToString().ToLowerInvariant()}-em-resposta",
                CalculadorDoRiscoCidadao.SeveridadePadrao(categoria, ExposicaoAchado.RespostaHttp),
                "runtime:response-guard", ExposicaoAchado.RespostaHttp, categoria));
        }

        var bytes = Encoding.UTF8.GetBytes(limpo.Texto);
        contexto.Response.ContentLength = bytes.Length;
        await original.WriteAsync(bytes, contexto.RequestAborted);
    }
}
