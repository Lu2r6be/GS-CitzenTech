using System.Diagnostics.CodeAnalysis;

namespace Termometro.Domain.Entidades;

/// <summary>
/// CPF válido (11 dígitos e dígitos verificadores corretos). Objeto de valor imutável.
/// <see cref="ToString"/> devolve a versão MASCARADA: sem querer, um CPF nunca vai inteiro
/// para um log ou para uma resposta. Quem precisa do número completo usa <see cref="Numeros"/>.
/// </summary>
public sealed record Cpf
{
    public string Numeros { get; }

    private Cpf(string numeros) => Numeros = numeros;

    /// <summary>Aceita "529.982.247-25" ou "52998224725".</summary>
    public static Cpf Criar(string? texto) =>
        TentarCriar(texto, out var cpf)
            ? cpf
            : throw new ArgumentException("CPF inválido.", nameof(texto));

    public static bool TentarCriar(string? texto, [NotNullWhen(true)] out Cpf? cpf)
    {
        cpf = null;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var numeros = new string(texto.Where(char.IsAsciiDigit).ToArray());
        if (numeros.Length != 11 || numeros.Distinct().Count() == 1)
        {
            return false;
        }
        if (!DigitoConfere(numeros, 9) || !DigitoConfere(numeros, 10))
        {
            return false;
        }

        cpf = new Cpf(numeros);
        return true;
    }

    /// <summary>Formato exibido em telas e respostas: ***.456.789-**</summary>
    public string Mascarado => $"***.{Numeros[3..6]}.{Numeros[6..9]}-**";

    public override string ToString() => Mascarado;

    // Dígito verificador: soma ponderada dos dígitos anteriores, do maior peso para o menor.
    private static bool DigitoConfere(string numeros, int posicao)
    {
        var soma = 0;
        for (var i = 0; i < posicao; i++)
        {
            soma += (numeros[i] - '0') * (posicao + 1 - i);
        }
        var resto = soma % 11;
        var digito = resto < 2 ? 0 : 11 - resto;
        return numeros[posicao] - '0' == digito;
    }
}
