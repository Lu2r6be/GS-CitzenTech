using System.Text;
using Termometro.Domain.Entidades;

namespace Termometro.Domain.Seguranca;

/// <summary>Texto já limpo + o que foi encontrado (só os tipos; nunca o valor).</summary>
public sealed record TextoSanitizado(string Texto, IReadOnlyList<OcorrenciaPii> Ocorrencias)
{
    public bool HouveSubstituicao => Ocorrencias.Count > 0;
    public IReadOnlyCollection<CategoriaPii> Categorias => Ocorrencias.Select(o => o.Categoria).Distinct().ToArray();
}

/// <summary>
/// O sanitizador: troca o dado sensível por um marcador. É o ÚNICO jeito aprovado de um dado
/// pessoal chegar a um log, uma resposta ou um prompt (nas regras de taint, ele "limpa" o dado).
/// Limpar um texto já limpo não muda nada (idempotente).
/// </summary>
public sealed class PiiSanitizer(DetectorDePii detector)
{
    public PiiSanitizer() : this(new DetectorDePii()) { }

    /// <summary>Remove só as categorias que a saída proíbe (ver <see cref="PoliticaDeSaida"/>).</summary>
    public TextoSanitizado Sanitizar(string? texto, ExposicaoAchado saida) =>
        Sanitizar(texto, PoliticaDeSaida.CategoriasProibidas(saida));

    public TextoSanitizado Sanitizar(string? texto, IReadOnlySet<CategoriaPii> categoriasProibidas)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return new TextoSanitizado(texto ?? string.Empty, []);
        }

        var aRemover = detector.Detectar(texto).Where(o => categoriasProibidas.Contains(o.Categoria)).ToList();
        if (aRemover.Count == 0)
        {
            return new TextoSanitizado(texto, []);
        }

        var saida = new StringBuilder(texto.Length);
        var cursor = 0;
        foreach (var o in aRemover)
        {
            saida.Append(texto, cursor, o.Inicio - cursor);
            saida.Append(Substituto(o, texto));
            cursor = o.Fim;
        }
        saida.Append(texto, cursor, texto.Length - cursor);
        return new TextoSanitizado(saida.ToString(), aRemover);
    }

    private static string Substituto(OcorrenciaPii o, string texto)
    {
        // CPF mantém o formato mascarado que a API já usa em qualquer tela: ***.456.789-**
        if (o.Categoria == CategoriaPii.Cpf && Cpf.TentarCriar(texto.Substring(o.Inicio, o.Tamanho), out var cpf))
        {
            return cpf.Mascarado;
        }

        return o.Categoria switch
        {
            CategoriaPii.Cpf => "[CPF-REMOVIDO]",
            CategoriaPii.Documento => "[DOCUMENTO-REMOVIDO]",
            CategoriaPii.Senha => "[SENHA-REMOVIDA]",
            CategoriaPii.Saude => "[SAUDE-REMOVIDA]",
            CategoriaPii.Segredo => "[SEGREDO-REMOVIDO]",
            CategoriaPii.Email => "[EMAIL-REMOVIDO]",
            CategoriaPii.Contato => "[CONTATO-REMOVIDO]",
            CategoriaPii.Endereco => "[ENDERECO-REMOVIDO]",
            _ => "[DADO-PESSOAL-REMOVIDO]",
        };
    }
}
