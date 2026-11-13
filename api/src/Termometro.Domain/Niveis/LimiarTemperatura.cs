namespace Termometro.Domain.Niveis;

public enum OrigemLimiar
{
    Regra = 0,
    ML = 1,
}

/// <summary>
/// Faixas do semáforo. Ficam no banco (tabela LimiarTemperatura), nunca espalhadas no código,
/// para que o ML ou a equipe de saúde possam ajustá-las sem recompilar.
/// </summary>
/// <remarks>
/// Azul: temperatura &lt; <see cref="AzulAbaixoDe"/>. Verde: índice de calor &lt; <see cref="VerdeAte"/>.
/// Amarelo: índice de calor &lt; <see cref="AmareloAte"/>. Vermelho: o resto.
/// </remarks>
public sealed record LimiarTemperatura
{
    public double AzulAbaixoDe { get; }
    public double VerdeAte { get; }
    public double AmareloAte { get; }
    public string Versao { get; }
    public OrigemLimiar Origem { get; }

    public LimiarTemperatura(double azulAbaixoDe, double verdeAte, double amareloAte,
        string versao, OrigemLimiar origem)
    {
        if (!(azulAbaixoDe < verdeAte && verdeAte < amareloAte))
        {
            throw new ArgumentException(
                "Os limiares precisam estar em ordem crescente: azul < verde < amarelo.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(versao);

        AzulAbaixoDe = azulAbaixoDe;
        VerdeAte = verdeAte;
        AmareloAte = amareloAte;
        Versao = versao;
        Origem = origem;
    }

    /// <summary>
    /// Valores iniciais. Calor: faixas do NWS (verificar fonte antes de citar no pitch).
    /// Frio: mínimo de 18 °C dentro de casa (referência OMS; conferir antes de citar).
    /// </summary>
    public static LimiarTemperatura Padrao { get; } =
        new(azulAbaixoDe: 18, verdeAte: 32, amareloAte: 39, versao: "regra-inicial", OrigemLimiar.Regra);
}
