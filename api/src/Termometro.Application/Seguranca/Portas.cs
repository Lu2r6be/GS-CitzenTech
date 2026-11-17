using Termometro.Domain.Entidades;
using Termometro.Domain.Seguranca;

namespace Termometro.Application.Seguranca;

/// <summary>Um achado ainda não gravado. Nunca carrega o dado sensível: só tipo, origem e gravidade.</summary>
public sealed record NovoAchado(
    string Tipo, SeveridadeAchado Severidade, string Origem, ExposicaoAchado Exposicao, CategoriaPii? Categoria);

public interface IAchadosRepositorio
{
    Task AdicionarAsync(IReadOnlyCollection<AchadoSeguranca> achados, CancellationToken ct);

    /// <summary>Apaga os achados cuja origem começa com o prefixo (ex.: "semgrep:"). Devolve quantos apagou.</summary>
    Task<int> RemoverPorPrefixoDeOrigemAsync(string prefixo, CancellationToken ct);
    Task<IReadOnlyList<AchadoSeguranca>> ListarDesdeAsync(DateTimeOffset desde, int limite, CancellationToken ct);
}

/// <summary>
/// Quem anota um achado SEM atrasar a requisição (o log e a resposta não esperam o banco).
/// A implementação enfileira e grava em segundo plano.
/// </summary>
public interface IRegistradorDeAchados
{
    void Registrar(NovoAchado achado);
}

/// <summary>O que o LLM recebe: só números e nomes de categorias. Nenhum dado de morador.</summary>
public sealed record ResumoParaAlerta(
    int Pontuacao,
    NivelDeRisco Nivel,
    int TotalDeAchados,
    int AbertosNoCodigo,
    int BarradosEmExecucao,
    IReadOnlyList<ContribuicaoDoGrupo> PorCategoria,
    IReadOnlyList<ContribuicaoDoGrupo> PorSaida);

/// <summary>O alerta em linguagem simples. <c>Fonte</c> diz quem escreveu: "mistral" ou "modelo-reserva".</summary>
public sealed record AlertaAoCidadao(string Titulo, string Mensagem, IReadOnlyList<string> Acoes, string Fonte);

public interface ITradutorDeAlerta
{
    Task<AlertaAoCidadao> TraduzirAsync(ResumoParaAlerta resumo, CancellationToken ct);
}
