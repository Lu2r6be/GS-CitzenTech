// GRUPO DE CONTROLE: o mesmo trabalho, do jeito certo. O Semgrep NAO deve acusar nada aqui.
using Microsoft.Extensions.Logging;

namespace Prototipo;

public sealed class ControleSeguro
{
    private readonly ILogger<ControleSeguro> log;
    private readonly PiiSanitizer sanitizador;

    public ControleSeguro(ILogger<ControleSeguro> log, PiiSanitizer sanitizador)
    {
        this.log = log;
        this.sanitizador = sanitizador;
    }

    public object Cadastrar(CadastrarResidentePedido pedido, Residente residente)
    {
        // Só identificadores e contagens no log
        log.LogInformation("Residente {Id} cadastrado na microárea {Microarea}", residente.Id, residente.MicroareaId);

        // CPF mascarado: ***.456.789-**
        log.LogInformation("Cadastro com CPF {Cpf}", residente.Cpf.Mascarado);

        // Texto livre passa pelo sanitizador antes de ir para o log
        log.LogWarning(sanitizador.Sanitizar(pedido.CondicoesSaude, ExposicaoAchado.Log).Texto);

        // Resposta com o CPF mascarado
        return Ok(new { residente.Id, Cpf = residente.Cpf.Mascarado });
    }

    public object Detalhar(Residente residente) => Ok(new { residente.Id, residente.MicroareaId });
}
