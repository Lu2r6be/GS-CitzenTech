using Termometro.Application.Abstracoes;
using Termometro.Application.Erros;
using Termometro.Domain.Entidades;
using Termometro.Domain.Seguranca;

namespace Termometro.Application.Seguranca;

// ---------- Importar achados das ferramentas (Semgrep, Trufflehog) ----------

public sealed record ItemDeAchado(string Tipo, string Severidade, string Origem, string Exposicao, string? CategoriaPii);

/// <param name="Achados">Os achados da varredura.</param>
/// <param name="SubstituirOrigem">
/// Opcional: "semgrep:" ou "trufflehog:". Antes de gravar, apaga os achados dessa ferramenta.
/// Uma varredura mostra a situação ATUAL do código; a nova substitui a anterior (corrigiu, o achado some).
/// Com este campo, a lista pode vir vazia (varredura limpa).
/// </param>
public sealed record ImportarAchadosPedido(IReadOnlyList<ItemDeAchado> Achados, string? SubstituirOrigem = null);

public sealed record ImportacaoConcluida(int Gravados, int Substituidos = 0);

/// <summary>
/// Recebe achados de ferramentas externas (Semgrep em modo taint, Trufflehog) e grava.
/// Os textos de "tipo" e "origem" também passam pelo detector: um achado nunca pode virar
/// um novo vazamento (ex.: o trecho de código com o CPF dentro).
/// </summary>
public sealed class ImportarAchados(IAchadosRepositorio repositorio, IUnidadeDeTrabalho unidade, TimeProvider relogio)
{
    public const int TamanhoMaximoDoLote = 500;
    private static readonly string[] OrigensSubstituiveis = ["semgrep:", "trufflehog:"];
    private readonly DetectorDePii _detector = new();

    public async Task<ImportacaoConcluida> ExecutarAsync(UsuarioAutenticado usuario, ImportarAchadosPedido pedido, CancellationToken ct)
    {
        Permissoes.ExigirPapel(usuario, PapelUsuario.Admin);
        var substituir = pedido.SubstituirOrigem?.Trim();
        if (substituir is not null && !OrigensSubstituiveis.Contains(substituir))
        {
            throw new DadosInvalidos("substituirOrigem deve ser \"semgrep:\" ou \"trufflehog:\".");
        }
        var itens = pedido.Achados ?? [];
        if (itens.Count == 0 && substituir is null)
        {
            throw new DadosInvalidos("Envie ao menos um achado.");
        }
        if (itens.Count > TamanhoMaximoDoLote)
        {
            throw new DadosInvalidos($"No máximo {TamanhoMaximoDoLote} achados por lote.");
        }
        // Só aceita substituir achados que vêm da MESMA ferramenta: nunca apaga o que foi barrado em execução.
        if (substituir is not null && itens.Any(i => !(i.Origem ?? string.Empty).StartsWith(substituir, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DadosInvalidos($"Com substituirOrigem, toda origem deve começar com \"{substituir}\".");
        }

        var agora = relogio.GetUtcNow();
        var novos = new List<AchadoSeguranca>(itens.Count);
        for (var i = 0; i < itens.Count; i++)
        {
            novos.Add(Converter(itens[i], i, agora));
        }

        // Valida tudo ANTES de apagar: um lote com erro não pode apagar a varredura anterior.
        var removidos = substituir is null ? 0 : await repositorio.RemoverPorPrefixoDeOrigemAsync(substituir, ct);
        if (novos.Count > 0)
        {
            await repositorio.AdicionarAsync(novos, ct);
            await unidade.SalvarAsync(ct);
        }
        return new ImportacaoConcluida(novos.Count, removidos);
    }

    private AchadoSeguranca Converter(ItemDeAchado item, int posicao, DateTimeOffset agora)
    {
        string Onde() => $"Achado {posicao + 1}: ";

        if (string.IsNullOrWhiteSpace(item.Tipo) || item.Tipo.Length > 100)
        {
            throw new DadosInvalidos(Onde() + "'tipo' é obrigatório e tem no máximo 100 caracteres.");
        }
        if (string.IsNullOrWhiteSpace(item.Origem) || item.Origem.Length > 200)
        {
            throw new DadosInvalidos(Onde() + "'origem' é obrigatória e tem no máximo 200 caracteres.");
        }
        // A mensagem de erro NÃO repete o texto recebido, justamente porque ele tem dado sensível.
        if (_detector.ContemPii(item.Tipo) || _detector.ContemPii(item.Origem))
        {
            throw new DadosInvalidos(Onde() + "'tipo' e 'origem' não podem conter dado pessoal ou segredo.");
        }
        if (!Enum.TryParse<SeveridadeAchado>(item.Severidade, ignoreCase: true, out var severidade))
        {
            throw new DadosInvalidos(Onde() + "severidade deve ser baixa, media, alta ou critica.");
        }
        if (!Enum.TryParse<ExposicaoAchado>(item.Exposicao, ignoreCase: true, out var exposicao))
        {
            throw new DadosInvalidos(Onde() + "exposicao deve ser log, respostaHttp, promptLlm ou arquivo.");
        }

        CategoriaPii? categoria = null;
        if (!string.IsNullOrWhiteSpace(item.CategoriaPii))
        {
            if (!Enum.TryParse<CategoriaPii>(item.CategoriaPii, ignoreCase: true, out var c))
            {
                throw new DadosInvalidos(Onde() + "categoriaPii desconhecida.");
            }
            categoria = c;
        }

        return new AchadoSeguranca(item.Tipo.Trim(), severidade, item.Origem.Trim(), exposicao, categoria, agora);
    }
}

// ---------- Painel Risco Cidadão ----------

public sealed record AchadoResumo(string Tipo, string Severidade, string Origem, string Exposicao, string? Categoria,
    bool BarradoEmExecucao, DateTimeOffset CriadoEm);

public sealed record PainelRiscoCidadao(
    int Pontuacao,
    string Nivel,
    int TotalDeAchados,
    int AbertosNoCodigo,
    int BarradosEmExecucao,
    IReadOnlyList<ContribuicaoDoGrupo> PorCategoria,
    IReadOnlyList<ContribuicaoDoGrupo> PorSaida,
    IReadOnlyList<AchadoResumo> Recentes,
    AlertaAoCidadao Alerta,
    int JanelaEmDias,
    DateTimeOffset GeradoEm);

/// <summary>Calcula o indicador Risco Cidadão com os achados dos últimos dias e pede o alerta ao tradutor.</summary>
public sealed class ConsultarRiscoCidadao(IAchadosRepositorio repositorio, ITradutorDeAlerta tradutor, TimeProvider relogio)
{
    public const int JanelaEmDias = 30;
    public const int LimiteDeAchados = 5000;
    private const int QuantosRecentes = 20;

    public async Task<PainelRiscoCidadao> ExecutarAsync(UsuarioAutenticado usuario, CancellationToken ct)
    {
        Permissoes.ExigirPapel(usuario, PapelUsuario.Admin);

        var agora = relogio.GetUtcNow();
        var achados = await repositorio.ListarDesdeAsync(agora.AddDays(-JanelaEmDias), LimiteDeAchados, ct);

        var resultado = CalculadorDoRiscoCidadao.Calcular(
            achados.Select(a => new AchadoParaRisco(a.Severidade, a.CategoriaPii, a.Exposicao, a.Origem)));

        var resumo = new ResumoParaAlerta(resultado.Pontuacao, resultado.Nivel, resultado.TotalDeAchados,
            resultado.AbertosNoCodigo, resultado.BarradosEmExecucao, resultado.PorCategoria, resultado.PorSaida);
        var alerta = await tradutor.TraduzirAsync(resumo, ct);

        var recentes = achados
            .OrderByDescending(a => a.CriadoEm)
            .Take(QuantosRecentes)
            .Select(a => new AchadoResumo(a.Tipo, a.Severidade.ToString(), a.Origem, a.Exposicao.ToString(),
                a.CategoriaPii?.ToString(),
                a.Origem.StartsWith(CalculadorDoRiscoCidadao.PrefixoDeOrigemEmExecucao, StringComparison.OrdinalIgnoreCase),
                a.CriadoEm))
            .ToList();

        return new PainelRiscoCidadao(resultado.Pontuacao, resultado.Nivel.ToString(), resultado.TotalDeAchados,
            resultado.AbertosNoCodigo, resultado.BarradosEmExecucao, resultado.PorCategoria, resultado.PorSaida,
            recentes, alerta, JanelaEmDias, agora);
    }
}

// ---------- Analisar um texto (motor de PII sob demanda) ----------

public sealed record AnalisarTextoPedido(string Texto, string? Saida);

public sealed record OcorrenciaDetectada(string Categoria, string Regra, int Inicio, int Tamanho);

public sealed record TextoAnalisado(IReadOnlyList<OcorrenciaDetectada> Ocorrencias, string TextoSanitizado, string Saida);

/// <summary>Mostra o motor de PII funcionando: o que ele acharia e como ficaria o texto depois do sanitizador.</summary>
public sealed class AnalisarTexto
{
    public const int TamanhoMaximo = 20_000;
    private readonly DetectorDePii _detector = new();
    private readonly PiiSanitizer _sanitizador = new();

    public TextoAnalisado Executar(UsuarioAutenticado usuario, AnalisarTextoPedido pedido)
    {
        Permissoes.ExigirPapel(usuario, PapelUsuario.Admin);
        if (string.IsNullOrWhiteSpace(pedido.Texto) || pedido.Texto.Length > TamanhoMaximo)
        {
            throw new DadosInvalidos($"Envie um texto de 1 a {TamanhoMaximo} caracteres.");
        }

        var saida = ExposicaoAchado.Log;
        if (!string.IsNullOrWhiteSpace(pedido.Saida) && !Enum.TryParse(pedido.Saida, ignoreCase: true, out saida))
        {
            throw new DadosInvalidos("saida deve ser log, respostaHttp, promptLlm ou arquivo.");
        }

        var ocorrencias = _detector.Detectar(pedido.Texto)
            .Select(o => new OcorrenciaDetectada(o.Categoria.ToString(), o.Regra, o.Inicio, o.Tamanho))
            .ToList();
        var limpo = _sanitizador.Sanitizar(pedido.Texto, saida);

        // Só inspeciona: analisar um texto não é um vazamento, então não gera achado.
        return new TextoAnalisado(ocorrencias, limpo.Texto, saida.ToString());
    }
}
