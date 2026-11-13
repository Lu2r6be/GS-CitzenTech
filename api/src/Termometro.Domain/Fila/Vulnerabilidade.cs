namespace Termometro.Domain.Fila;

/// <summary>
/// Quanto a pessoa é vulnerável. NÃO muda a cor do semáforo (a cor é "o que o sensor mede"):
/// só a ORDEM da fila de visitas.
/// </summary>
public sealed record Vulnerabilidade(int Idade, bool MoraSozinho, bool TemDoencaCronica)
{
    public const int IdadeDeRisco = 80;

    public int Pontos =>
        (Idade >= IdadeDeRisco ? 1 : 0) + (MoraSozinho ? 1 : 0) + (TemDoencaCronica ? 1 : 0);
}
