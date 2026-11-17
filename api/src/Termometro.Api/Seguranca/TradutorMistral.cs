using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Termometro.Application.Seguranca;

namespace Termometro.Api.Seguranca;

public sealed class OpcoesMistral
{
    public const string Secao = "Mistral";

    /// <summary>Chave da API. SEMPRE por variável de ambiente (Mistral__ApiKey); nunca no repositório.</summary>
    public string ApiKey { get; set; } = string.Empty;
    public string Modelo { get; set; } = "mistral-small-latest";
    public string Endereco { get; set; } = "https://api.mistral.ai/v1/chat/completions";
    public int TempoLimiteEmSegundos { get; set; } = 15;
}

/// <summary>
/// Pede à Mistral que reescreva o resumo técnico como alerta para o cidadão.
/// O prompt leva só números e nomes de categorias (nenhum dado de morador). O resultado é conferido
/// e, se falhar, o <see cref="TradutorComReserva"/> usa o texto-modelo.
/// </summary>
public sealed class TradutorMistral(HttpClient http, Microsoft.Extensions.Options.IOptions<OpcoesMistral> opcoes) : ITradutorDeAlerta
{
    public const string Fonte = "mistral";
    private static readonly Termometro.Domain.Seguranca.DetectorDePii Detector = new();

    private const string Instrucoes =
        "Você escreve alertas de segurança para cidadãos comuns, em português do Brasil, sem jargão técnico. "
        + "Receberá um resumo numérico de falhas de proteção de dados de um sistema de saúde da família. "
        + "Explique o risco real para a pessoa (golpe, fraude, constrangimento), diga o que o sistema está fazendo "
        + "e o que a pessoa pode fazer. NUNCA invente nomes, CPFs, endereços ou números além dos recebidos. "
        + "Respeite a linha 'Situação': 'barrado em execução' quer dizer que o sistema impediu a tempo e nada saiu; "
        + "'falha aberta no código' é um risco que ainda precisa ser corrigido. Se não houver falhas abertas, "
        + "NÃO diga que dados foram ou podem ter sido expostos: diga que o sistema bloqueou as tentativas e dê dicas de prevenção. "
        + "Responda SOMENTE um objeto JSON: {\"titulo\": texto curto, \"mensagem\": até 3 frases, \"acoes\": lista de 2 a 4 frases curtas}.";

    public async Task<AlertaAoCidadao> TraduzirAsync(ResumoParaAlerta resumo, CancellationToken ct)
    {
        var o = opcoes.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey))
        {
            throw new InvalidOperationException("Mistral:ApiKey não configurada.");
        }

        // Sumidouro "prompt do LLM": se o texto a enviar tiver dado sensível, não envia (a reserva assume).
        var prompt = DescreverResumo(resumo);
        if (Detector.ContemPii(prompt) || Detector.ContemPii(Instrucoes))
        {
            throw new InvalidOperationException("O prompt continha dado sensível e não foi enviado.");
        }

        using var tempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tempo.CancelAfter(TimeSpan.FromSeconds(o.TempoLimiteEmSegundos));

        using var pedido = new HttpRequestMessage(HttpMethod.Post, o.Endereco)
        {
            Content = JsonContent.Create(new
            {
                model = o.Modelo,
                temperature = 0.2,
                max_tokens = 500,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = Instrucoes },
                    new { role = "user", content = prompt },
                },
            }),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);

        using var resposta = await http.SendAsync(pedido, tempo.Token);
        resposta.EnsureSuccessStatusCode();

        var json = await resposta.Content.ReadFromJsonAsync<RespostaDaMistral>(cancellationToken: tempo.Token)
                   ?? throw new InvalidOperationException("Resposta vazia da Mistral.");
        var conteudo = json.Choices?.FirstOrDefault()?.Message?.Content
                       ?? throw new InvalidOperationException("Resposta sem conteúdo.");

        var alerta = JsonSerializer.Deserialize<AlertaJson>(conteudo, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                     ?? throw new InvalidOperationException("Alerta ilegível.");
        return new AlertaAoCidadao(alerta.Titulo ?? string.Empty, alerta.Mensagem ?? string.Empty,
            alerta.Acoes?.Where(a => !string.IsNullOrWhiteSpace(a)).Take(4).ToList() ?? [], Fonte);
    }

    /// <summary>Texto enviado à IA. Só contagens e nomes de tipos de dado.</summary>
    internal static string DescreverResumo(ResumoParaAlerta r)
    {
        string Lista(IEnumerable<Termometro.Domain.Seguranca.ContribuicaoDoGrupo> g) =>
            string.Join(", ", g.Select(x => $"{x.Grupo}: {x.Quantidade}"));

        var situacao = r.TotalDeAchados == 0
            ? "nenhuma falha registrada; os dados estão protegidos."
            : r.AbertosNoCodigo == 0
                ? "todas as tentativas foram barradas antes de sair; nenhum dado chegou a ser exposto."
                : "há falhas abertas no código que ainda precisam ser corrigidas; até lá, podem expor dados.";

        return $"Situação: {situacao} "
               + $"Indicador Risco Cidadão: {r.Pontuacao} de 100 (nível {r.Nivel}). "
               + $"Total de achados: {r.TotalDeAchados}; falhas abertas no código: {r.AbertosNoCodigo}; "
               + $"vazamentos barrados em execução: {r.BarradosEmExecucao}. "
               + $"Tipos de dado envolvidos: {Lista(r.PorCategoria)}. Por onde iriam vazar: {Lista(r.PorSaida)}.";
    }

    private sealed record RespostaDaMistral([property: JsonPropertyName("choices")] List<Escolha>? Choices);
    private sealed record Escolha([property: JsonPropertyName("message")] Mensagem? Message);
    private sealed record Mensagem([property: JsonPropertyName("content")] string? Content);
    private sealed record AlertaJson(string? Titulo, string? Mensagem, List<string>? Acoes);
}
