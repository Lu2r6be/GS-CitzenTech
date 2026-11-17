using Termometro.Domain.Entidades;

namespace Termometro.Domain.Seguranca;

/// <summary>
/// Regra do que NUNCA pode sair por cada "saída insegura" (os sumidouros do taint analysis).
///  - Log: nada de dado pessoal nem segredo (o log é lido por muita gente e copiado para backups).
///  - Resposta HTTP: CPF inteiro, RG e senha nunca. Saúde e e-mail podem, para quem tem permissão
///    (o ACS precisa ver as condições do morador); o token do login também pode.
///  - Prompt do LLM e arquivo: nada; o texto vai para fora do nosso controle.
/// </summary>
public static class PoliticaDeSaida
{
    private static readonly IReadOnlySet<CategoriaPii> TodasAsCategorias =
        new HashSet<CategoriaPii>(Enum.GetValues<CategoriaPii>());

    private static readonly IReadOnlySet<CategoriaPii> ProibidasNaResposta =
        new HashSet<CategoriaPii> { CategoriaPii.Cpf, CategoriaPii.Senha, CategoriaPii.Documento };

    public static IReadOnlySet<CategoriaPii> CategoriasProibidas(ExposicaoAchado saida) => saida switch
    {
        ExposicaoAchado.RespostaHttp => ProibidasNaResposta,
        _ => TodasAsCategorias,
    };
}
