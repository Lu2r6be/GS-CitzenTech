using System.Text.RegularExpressions;
using Termometro.Domain.Entidades;

namespace Termometro.Domain.Seguranca;

/// <summary>
/// O "motor de parsing" de PII: percorre um texto e acha CPF, senha, condição de saúde, segredo,
/// e-mail, telefone e endereço. Função pura (sem banco, sem log): fácil de testar.
///
/// Cada regra é um padrão (regex) com, quando precisa, uma conferência a mais. O CPF, por exemplo,
/// só conta se os dígitos verificadores batem; 11 números quaisquer não são CPF.
/// </summary>
public sealed class DetectorDePii
{
    private static readonly TimeSpan LimiteDeTempo = TimeSpan.FromMilliseconds(250);

    private delegate IEnumerable<(int Inicio, int Tamanho)> Localizador(string texto);

    private sealed record Regra(string Nome, CategoriaPii Categoria, Localizador Localizar);

    private static Regex Padrao(string padrao) =>
        new(padrao, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, LimiteDeTempo);

    private static readonly Regex PadraoCpf = Padrao(@"(?<!\d)\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?!\d)");

    // "senha: abc123", "password=abc", "\"senha\":\"abc\"". Pega só o VALOR (grupo "valor").
    // Valores já trocados por marcadores ("[SENHA-REMOVIDA]") não contam, senão limpar um texto o acusaria de novo.
    private static readonly Regex PadraoSenha = Padrao(
        @"\b(?:senha|password|passwd|pwd)\b[""']?\s*[:=]\s*[""']?(?<valor>[^\s""',;&}\[\]]{3,})");

    private static readonly Regex PadraoSaude = Padrao(
        @"\b(?:diabet\w*|hipertens[aã]o|hipertens\w+|cardiopatia|insufici[eê]ncia\s+(?:card[ií]aca|renal)|alzheimer|parkinson|"
        + @"c[aâ]ncer|avc|derrame|infarto|hiv|aids|depress[aã]o|esquizofrenia|asma|dpoc|epilepsia|dem[eê]ncia|"
        + @"cid[-\s]?10\s*:?\s*[a-z]\d{2}(?:\.\d{1,2})?)\b");

    // RG não tem dígito verificador padrão nacional, então só conta COM RÓTULO: "RG: 12.345.678-9", "identidade 123456789",
    // "\"rg\":\"12345678X\"". O número solto não é acusado (evita falso positivo em qualquer sequência de dígitos).
    private static readonly Regex PadraoRg = Padrao(
        @"\b(?:rg|r\.g\.|identidade|registro\s+geral)\b[""']?\s*(?:n[º°o.]*\s*)?[:=]?\s*[""']?(?<valor>\d{1,2}\.?\d{3}\.?\d{3}-?[0-9xX])(?![\d])");

    private static readonly Regex PadraoJwt = Padrao(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]*");

    private static readonly Regex PadraoChave = Padrao(
        @"(?:\b(?:api[_-]?key|x-device-key|mistral_api_key|secret|token)\b[""']?\s*[:=]\s*[""']?(?<valor>[A-Za-z0-9_\-./+]{12,}))"
        + @"|(?:\bBearer\s+(?<valor>[A-Za-z0-9_\-./+=]{16,}))"
        + @"|(?:\bAKIA[0-9A-Z]{16}\b)"
        + @"|(?:-----BEGIN [A-Z ]*PRIVATE KEY-----)");

    private static readonly Regex PadraoEmail = Padrao(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}");

    private static readonly Regex PadraoTelefone = Padrao(@"(?<!\d)(?:\(\d{2}\)\s?9?\d{4}-?\d{4}|\d{2}\s?9\d{4}-?\d{4})(?!\d)");

    private static readonly Regex PadraoEndereco = Padrao(
        @"\b(?:rua|av\.?|avenida|travessa|alameda|estrada|r\.)\s+[^,\n;]{3,60},?\s*(?:n[º°o.]*\s*)?\d{1,5}\b");

    private static readonly IReadOnlyList<Regra> Regras =
    [
        new("cpf-com-digito-verificador", CategoriaPii.Cpf, LocalizarCpf),
        new("rg-com-rotulo", CategoriaPii.Documento, t => Grupo(PadraoRg, t, "valor", _ => true)),
        new("senha-em-texto", CategoriaPii.Senha, t => Grupo(PadraoSenha, t, "valor", ValorNaoEMarcador)),
        new("condicao-de-saude", CategoriaPii.Saude, t => Todos(PadraoSaude, t)),
        new("jwt", CategoriaPii.Segredo, t => Todos(PadraoJwt, t)),
        new("chave-ou-token", CategoriaPii.Segredo, t => Grupo(PadraoChave, t, "valor", ValorNaoEMarcador)),
        new("email", CategoriaPii.Email, t => Todos(PadraoEmail, t)),
        new("telefone-br", CategoriaPii.Contato, t => Todos(PadraoTelefone, t)),
        new("endereco-com-numero", CategoriaPii.Endereco, t => Todos(PadraoEndereco, t)),
    ];

    /// <summary>Acha todas as ocorrências, em ordem de posição. Sobreposições ficam com a maior.</summary>
    public IReadOnlyList<OcorrenciaPii> Detectar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return [];
        }

        var achadas = new List<OcorrenciaPii>();
        foreach (var regra in Regras)
        {
            try
            {
                foreach (var (inicio, tamanho) in regra.Localizar(texto))
                {
                    achadas.Add(new OcorrenciaPii(regra.Categoria, regra.Nome, inicio, tamanho));
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Texto feito para travar a regex (ReDoS): melhor tratar o texto TODO como suspeito do que ignorá-lo.
                achadas.Add(new OcorrenciaPii(regra.Categoria, regra.Nome + "-tempo-esgotado", 0, texto.Length));
            }
        }

        return RemoverSobreposicoes(achadas);
    }

    public bool ContemPii(string? texto) => Detectar(texto).Count > 0;

    private static IEnumerable<(int, int)> LocalizarCpf(string texto)
    {
        foreach (Match m in PadraoCpf.Matches(texto))
        {
            if (Cpf.TentarCriar(m.Value, out _))
            {
                yield return (m.Index, m.Length);
            }
        }
    }

    private static IEnumerable<(int, int)> Todos(Regex regex, string texto) =>
        regex.Matches(texto).Select(m => (m.Index, m.Length));

    private static IEnumerable<(int, int)> Grupo(Regex regex, string texto, string grupo, Func<string, bool> aceitar)
    {
        foreach (Match m in regex.Matches(texto))
        {
            var g = m.Groups[grupo];
            if (g.Success)
            {
                if (aceitar(g.Value))
                {
                    yield return (g.Index, g.Length);
                }
            }
            else
            {
                yield return (m.Index, m.Length); // alternativas sem grupo (ex.: AKIA..., chave privada)
            }
        }
    }

    private static bool ValorNaoEMarcador(string valor) => !valor.StartsWith('[');

    private static List<OcorrenciaPii> RemoverSobreposicoes(List<OcorrenciaPii> todas)
    {
        var resultado = new List<OcorrenciaPii>();
        foreach (var o in todas.OrderBy(o => o.Inicio).ThenByDescending(o => o.Tamanho))
        {
            if (resultado.Count > 0 && o.Inicio < resultado[^1].Fim)
            {
                continue; // já coberta por uma ocorrência anterior (a maior das duas)
            }
            resultado.Add(o);
        }
        return resultado;
    }
}
