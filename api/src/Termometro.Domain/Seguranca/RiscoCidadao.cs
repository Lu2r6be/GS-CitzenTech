using Termometro.Domain.Entidades;

namespace Termometro.Domain.Seguranca;

public enum NivelDeRisco { Baixo = 0, Moderado = 1, Alto = 2, Critico = 3 }

/// <summary>Só o que o cálculo precisa de um achado. Sem nenhum dado pessoal.</summary>
public sealed record AchadoParaRisco(
    SeveridadeAchado Severidade, CategoriaPii? Categoria, ExposicaoAchado Exposicao, string Origem);

public sealed record ContribuicaoDoGrupo(string Grupo, int Quantidade, double Pontos);

public sealed record ResultadoDoRiscoCidadao(
    int Pontuacao,
    NivelDeRisco Nivel,
    int TotalDeAchados,
    int AbertosNoCodigo,
    int BarradosEmExecucao,
    IReadOnlyList<ContribuicaoDoGrupo> PorCategoria,
    IReadOnlyList<ContribuicaoDoGrupo> PorSaida);

/// <summary>
/// O indicador "Risco Cidadão": de 0 a 100, quanto os cidadãos cadastrados estão expostos a
/// vazamento de dado pessoal por falhas deste sistema. NÃO é o risco térmico da casa.
///
/// Cada achado vale: peso = gravidade x sensibilidade do dado x gravidade da saída x situação.
///   gravidade:     baixa 1, média 2, alta 4, crítica 8
///   sensibilidade: senha 3, segredo 3, saúde 3, CPF e RG 2,5, endereço 2, contato 1,5, e-mail 1,5, nome 1 (desconhecido 1,5)
///   saída:         log 1, arquivo 1, resposta HTTP 1,5, prompt do LLM 2 (o texto sai do nosso controle)
///   situação:      falha ABERTA no código (análise estática) 1; vazamento BARRADO em execução 0,25
///                  (o dado não saiu, mas provou que alguma parte do código tenta soltá-lo)
/// A soma dos pesos vira nota: 100 x (1 - e^(-soma/60)). Cresce rápido no começo e nunca passa de 100.
/// Faixas: 0-19 baixo, 20-49 moderado, 50-74 alto, 75-100 crítico.
/// </summary>
public static class CalculadorDoRiscoCidadao
{
    public const double Escala = 60.0;
    public const double FatorBarradoEmExecucao = 0.25;
    public const string PrefixoDeOrigemEmExecucao = "runtime:";

    public static ResultadoDoRiscoCidadao Calcular(IEnumerable<AchadoParaRisco> achados)
    {
        var lista = achados.ToList();
        var pesos = lista.Select(a => (Achado: a, Peso: PesoDe(a))).ToList();
        var soma = pesos.Sum(p => p.Peso);
        var pontuacao = (int)Math.Round(100 * (1 - Math.Exp(-soma / Escala)), MidpointRounding.AwayFromZero);
        pontuacao = Math.Clamp(pontuacao, 0, 100);

        var barrados = lista.Count(EstaBarradoEmExecucao);

        return new ResultadoDoRiscoCidadao(
            pontuacao,
            NivelPara(pontuacao),
            lista.Count,
            lista.Count - barrados,
            barrados,
            Agrupar(pesos, p => p.Achado.Categoria?.ToString() ?? "Outro"),
            Agrupar(pesos, p => p.Achado.Exposicao.ToString()));
    }

    public static NivelDeRisco NivelPara(int pontuacao) => pontuacao switch
    {
        < 20 => NivelDeRisco.Baixo,
        < 50 => NivelDeRisco.Moderado,
        < 75 => NivelDeRisco.Alto,
        _ => NivelDeRisco.Critico,
    };

    public static double PesoDe(AchadoParaRisco a) =>
        PesoDaSeveridade(a.Severidade) * PesoDaCategoria(a.Categoria) * PesoDaSaida(a.Exposicao)
        * (EstaBarradoEmExecucao(a) ? FatorBarradoEmExecucao : 1.0);

    public static bool EstaBarradoEmExecucao(AchadoParaRisco a) =>
        a.Origem.StartsWith(PrefixoDeOrigemEmExecucao, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gravidade padrão de um vazamento: depende do dado e de para onde ele iria.</summary>
    public static SeveridadeAchado SeveridadePadrao(CategoriaPii? categoria, ExposicaoAchado saida)
    {
        var baseDoDado = categoria switch
        {
            CategoriaPii.Senha or CategoriaPii.Segredo => SeveridadeAchado.Critica,
            CategoriaPii.Saude or CategoriaPii.Cpf or CategoriaPii.Documento => SeveridadeAchado.Alta,
            CategoriaPii.Endereco or CategoriaPii.Contato or CategoriaPii.Email => SeveridadeAchado.Media,
            _ => SeveridadeAchado.Baixa,
        };
        // Para o prompt de um LLM externo o dado sai do nosso controle: sobe um degrau.
        return saida == ExposicaoAchado.PromptLlm && baseDoDado < SeveridadeAchado.Critica ? baseDoDado + 1 : baseDoDado;
    }

    private static double PesoDaSeveridade(SeveridadeAchado s) => s switch
    {
        SeveridadeAchado.Baixa => 1,
        SeveridadeAchado.Media => 2,
        SeveridadeAchado.Alta => 4,
        _ => 8,
    };

    private static double PesoDaCategoria(CategoriaPii? c) => c switch
    {
        CategoriaPii.Senha or CategoriaPii.Segredo or CategoriaPii.Saude => 3,
        CategoriaPii.Cpf or CategoriaPii.Documento => 2.5,
        CategoriaPii.Endereco => 2,
        CategoriaPii.Contato or CategoriaPii.Email => 1.5,
        CategoriaPii.Nome => 1,
        _ => 1.5,
    };

    private static double PesoDaSaida(ExposicaoAchado e) => e switch
    {
        ExposicaoAchado.RespostaHttp => 1.5,
        ExposicaoAchado.PromptLlm => 2,
        _ => 1,
    };

    private static IReadOnlyList<ContribuicaoDoGrupo> Agrupar(
        List<(AchadoParaRisco Achado, double Peso)> pesos, Func<(AchadoParaRisco Achado, double Peso), string> chave) =>
        pesos.GroupBy(chave)
            .Select(g => new ContribuicaoDoGrupo(g.Key, g.Count(), Math.Round(g.Sum(p => p.Peso), 2)))
            .OrderByDescending(g => g.Pontos)
            .ToList();
}
