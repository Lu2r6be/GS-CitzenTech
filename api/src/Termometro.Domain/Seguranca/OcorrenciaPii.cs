namespace Termometro.Domain.Seguranca;

/// <summary>
/// Onde um dado sensível apareceu num texto. Guarda só a POSIÇÃO e o tipo, nunca o valor:
/// assim o próprio resultado da detecção não vira um novo vazamento.
/// </summary>
public sealed record OcorrenciaPii(CategoriaPii Categoria, string Regra, int Inicio, int Tamanho)
{
    public int Fim => Inicio + Tamanho;
}
