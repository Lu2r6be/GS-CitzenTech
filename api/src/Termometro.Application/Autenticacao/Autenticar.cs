using Termometro.Application.Abstracoes;
using Termometro.Application.Erros;

namespace Termometro.Application.Autenticacao;

public sealed record LoginPedido(string Email, string Senha);

public sealed record LoginResposta(string Token, DateTimeOffset ExpiraEm, string Papel);

public sealed class Autenticar(ICadastroRepositorio cadastro, IHashDeSenha hashDeSenha, IEmissorDeToken emissor)
{
    // Hash de mentira, só para gastar o mesmo tempo quando o e-mail não existe
    // (senão quem mede o tempo da resposta descobre quais e-mails são cadastrados).
    private readonly Lazy<string> _hashFalso = new(() => hashDeSenha.Gerar("senha-que-ninguem-usa"));

    public async Task<LoginResposta> ExecutarAsync(LoginPedido pedido, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pedido.Email) || string.IsNullOrEmpty(pedido.Senha))
        {
            throw new NaoAutenticado("E-mail ou senha incorretos.");
        }

        var usuario = await cadastro.ObterUsuarioPorEmailAsync(pedido.Email.Trim().ToLowerInvariant(), ct);
        var hash = usuario?.SenhaHash ?? _hashFalso.Value;
        var senhaConfere = hashDeSenha.Verificar(pedido.Senha, hash);

        if (usuario is null || !senhaConfere)
        {
            throw new NaoAutenticado("E-mail ou senha incorretos.");
        }

        var token = emissor.Emitir(usuario);
        return new LoginResposta(token.Token, token.ExpiraEm, usuario.Papel.ToString());
    }
}
