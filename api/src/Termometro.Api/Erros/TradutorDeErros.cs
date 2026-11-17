using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Termometro.Application.Erros;

namespace Termometro.Api.Erros;

/// <summary>
/// Traduz os erros dos casos de uso em respostas HTTP padrão (ProblemDetails).
/// Erro inesperado vira 500 SEM detalhes: a mensagem interna pode citar dados do banco.
/// </summary>
public sealed class TradutorDeErros(ILogger<TradutorDeErros> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception erro, CancellationToken ct)
    {
        var (status, titulo, detalhe) = erro switch
        {
            DadosInvalidos e => (StatusCodes.Status400BadRequest, "Dados inválidos", e.Message),
            NaoAutenticado e => (StatusCodes.Status401Unauthorized, "Não autenticado", e.Message),
            AcessoNegado e => (StatusCodes.Status403Forbidden, "Acesso negado", e.Message),
            NaoEncontrado e => (StatusCodes.Status404NotFound, "Não encontrado", e.Message),
            Conflito e => (StatusCodes.Status409Conflict, "Conflito", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Algo deu errado. Tente novamente."),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            // Registra o tipo e o rastro do erro; nunca o corpo da requisição (pode ter PII).
            log.LogError(erro, "Erro inesperado em {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);
        }

        contexto.Response.StatusCode = status;
        await contexto.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = titulo, Detail = detalhe }, ct);
        return true;
    }
}
