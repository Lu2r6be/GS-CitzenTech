// PROTOTIPO VULNERAVEL: nao compilar. Veja README.md desta pasta.
using Microsoft.Extensions.Logging;

namespace Prototipo;

public sealed class LoginInseguro
{
    private readonly ILogger<LoginInseguro> log;
    private readonly OpcoesDoServidor opcoes;

    public LoginInseguro(ILogger<LoginInseguro> log, OpcoesDoServidor opcoes)
    {
        this.log = log;
        this.opcoes = opcoes;
    }

    public void Entrar(LoginPedido pedido)
    {
        // VULNERAVEL 8: e-mail e SENHA em texto puro no log de depuração
        log.LogDebug("Tentativa de login {Email} senha {Senha}", pedido.Email, pedido.Senha);
    }

    public void Iniciar()
    {
        // VULNERAVEL 9: chave de acesso do dispositivo escrita no log de partida
        log.LogInformation("Servidor iniciado com a chave {Chave}", opcoes.ChaveDoDispositivo);
    }
}
