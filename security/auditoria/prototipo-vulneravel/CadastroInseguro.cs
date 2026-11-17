// PROTOTIPO VULNERAVEL: nao compilar. Veja README.md desta pasta.
using Microsoft.Extensions.Logging;

namespace Prototipo;

public sealed class CadastroInseguro
{
    private readonly ILogger<CadastroInseguro> log;
    private readonly HttpClient http;

    public CadastroInseguro(ILogger<CadastroInseguro> log, HttpClient http)
    {
        this.log = log;
        this.http = http;
    }

    public async Task<object> Cadastrar(CadastrarResidentePedido pedido, Residente residente)
    {
        // VULNERAVEL 1: CPF inteiro no log (CWE-532)
        log.LogInformation("Cadastrando residente com CPF {Cpf}", pedido.Cpf);

        // VULNERAVEL 2: nome e condição de saúde numa mensagem de log interpolada
        log.LogWarning($"Falha ao cadastrar {residente.Nome}: {residente.CondicoesSaude}");

        var resposta = new { residente.Id };

        // VULNERAVEL 4: a mensagem de erro vira o "detalhe" da resposta e leva o CPF junto
        if (await CpfJaExiste(pedido))
        {
            throw new Conflito($"CPF {pedido.Cpf} já cadastrado");
        }

        // VULNERAVEL 5: condição de saúde enviada num prompt para um serviço de IA externo
        await http.PostAsJsonAsync("https://api.exemplo-ia.test/v1/chat",
            new { prompt = $"Resuma o caso: {residente.CondicoesSaude}" });

        // VULNERAVEL 6: endereço do morador num log de console
        Console.WriteLine($"Visita agendada em {residente.Endereco}");

        // VULNERAVEL 12: RG (documento de identidade) num log de aviso
        log.LogWarning("Documento recebido: {Rg}", pedido.Rg);

        // VULNERAVEL 3: CPF inteiro na URL de criação (vai para o cabeçalho Location e para logs de proxy)
        return CreatedAtAction(nameof(Detalhar), new { cpf = residente.Cpf }, resposta);
    }

    public object Detalhar(Residente residente)
    {
        // VULNERAVEL 7: CPF inteiro na resposta (Ok com o objeto direto)
        return Ok(new { residente.Id, residente.Cpf, residente.TelefoneContato });
    }

    private Task<bool> CpfJaExiste(CadastrarResidentePedido pedido) => Task.FromResult(false);
}
