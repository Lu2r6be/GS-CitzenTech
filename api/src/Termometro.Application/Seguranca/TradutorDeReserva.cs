using Termometro.Domain.Seguranca;

namespace Termometro.Application.Seguranca;

/// <summary>
/// Texto-modelo que traduz o resumo técnico em alerta para o cidadão SEM usar IA.
/// Serve de reserva quando o LLM está fora do ar, sem chave ou devolve algo inaceitável:
/// o painel nunca fica sem explicação.
/// </summary>
public sealed class TradutorDeReserva : ITradutorDeAlerta
{
    public const string Fonte = "modelo-reserva";

    public Task<AlertaAoCidadao> TraduzirAsync(ResumoParaAlerta resumo, CancellationToken ct) =>
        Task.FromResult(Montar(resumo));

    public static AlertaAoCidadao Montar(ResumoParaAlerta r)
    {
        if (r.TotalDeAchados == 0)
        {
            return new AlertaAoCidadao(
                "Nenhum risco aos seus dados foi encontrado",
                "Até agora o sistema não registrou nenhuma tentativa de deixar dados pessoais escaparem. "
                + "Seus dados continuam protegidos e a vigilância segue ligada.",
                ["Mantenha seu cadastro atualizado.", "Se desconfiar de uma mensagem ou ligação pedindo seu CPF, fale com a unidade de saúde."],
                Fonte);
        }

        var dados = DescreverDados(r);
        var onde = DescreverSaidas(r);
        var risco = DescreverRiscoReal(r);

        var titulo = r.Nivel switch
        {
            NivelDeRisco.Critico => "Risco crítico: seus dados podem estar expostos",
            NivelDeRisco.Alto => "Risco alto: dados de moradores precisam de correção urgente",
            NivelDeRisco.Moderado when r.AbertosNoCodigo == 0 => "Risco moderado: tentativas de vazamento foram barradas a tempo",
            NivelDeRisco.Moderado => "Risco moderado: falhas encontradas, ainda sem vazamento confirmado",
            _ => "Risco baixo: o sistema barrou as tentativas de vazamento",
        };

        var situacao = r.AbertosNoCodigo > 0
            ? $"Encontramos {r.AbertosNoCodigo} falha(s) no código que ainda permitem que {dados} escapem {onde}. "
            : $"O sistema impediu a tempo {r.BarradosEmExecucao} tentativa(s) de deixar {dados} escaparem {onde}. ";

        var mensagem = situacao + risco + $" Pontuação do Risco Cidadão: {r.Pontuacao} de 100.";

        var acoes = new List<string>();
        if (r.AbertosNoCodigo > 0)
        {
            acoes.Add("A equipe deve corrigir as falhas abertas antes de novos cadastros.");
        }
        acoes.Add("Desconfie de contato que cite seu CPF ou sua saúde sem você ter pedido.");
        acoes.Add("Em caso de dúvida, procure a unidade de saúde e confirme o que foi cadastrado em seu nome.");
        if (r.Nivel >= NivelDeRisco.Alto)
        {
            acoes.Add("Troque senhas que use no sistema e não repita senhas de outros serviços.");
        }

        return new AlertaAoCidadao(titulo, mensagem, acoes, Fonte);
    }

    private static string DescreverDados(ResumoParaAlerta r)
    {
        var nomes = r.PorCategoria.Take(3).Select(g => g.Grupo switch
        {
            nameof(CategoriaPii.Cpf) => "CPF",
            nameof(CategoriaPii.Documento) => "RG",
            nameof(CategoriaPii.Saude) => "informações de saúde",
            nameof(CategoriaPii.Senha) => "senhas",
            nameof(CategoriaPii.Segredo) => "chaves de acesso",
            nameof(CategoriaPii.Endereco) => "endereços",
            nameof(CategoriaPii.Contato) => "telefones",
            nameof(CategoriaPii.Email) => "e-mails",
            nameof(CategoriaPii.Nome) => "nomes",
            _ => "dados pessoais",
        }).Distinct().ToList();

        return nomes.Count switch
        {
            0 => "dados pessoais",
            1 => nomes[0],
            _ => string.Join(", ", nomes.Take(nomes.Count - 1)) + " e " + nomes[^1],
        };
    }

    private static string DescreverSaidas(ResumoParaAlerta r)
    {
        var saidas = r.PorSaida.Take(2).Select(g => g.Grupo switch
        {
            nameof(Domain.Entidades.ExposicaoAchado.Log) => "para os registros do sistema",
            nameof(Domain.Entidades.ExposicaoAchado.RespostaHttp) => "para telas e respostas do aplicativo",
            nameof(Domain.Entidades.ExposicaoAchado.PromptLlm) => "para um serviço de inteligência artificial externo",
            nameof(Domain.Entidades.ExposicaoAchado.Arquivo) => "para arquivos",
            _ => "para fora do sistema",
        }).Distinct().ToList();

        return saidas.Count == 0 ? "para fora do sistema" : string.Join(" e ", saidas);
    }

    private static string DescreverRiscoReal(ResumoParaAlerta r)
    {
        var categorias = r.PorCategoria.Select(g => g.Grupo).ToHashSet();
        var riscos = new List<string>();
        if (categorias.Contains(nameof(CategoriaPii.Cpf)) || categorias.Contains(nameof(CategoriaPii.Documento)))
        {
            riscos.Add("com o CPF ou o RG alguém pode tentar abrir contas ou aplicar golpes em seu nome");
        }
        if (categorias.Contains(nameof(CategoriaPii.Saude)))
        {
            riscos.Add("informações de saúde expostas podem causar constrangimento e discriminação");
        }
        if (categorias.Contains(nameof(CategoriaPii.Senha)) || categorias.Contains(nameof(CategoriaPii.Segredo)))
        {
            riscos.Add("senhas e chaves permitem entrar no sistema como se fosse você");
        }
        if (riscos.Count == 0)
        {
            return "Dados expostos facilitam golpes e contatos indevidos.";
        }
        return "Na prática: " + string.Join("; ", riscos) + ".";
    }
}
