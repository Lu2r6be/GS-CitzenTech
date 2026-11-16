namespace Termometro.Domain.Entidades;

/// <summary>Território de um agente comunitário de saúde (ACS): ele só vê as casas da sua microárea.</summary>
public sealed class Microarea
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;

    private Microarea() { }

    public Microarea(string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        Id = Guid.CreateVersion7();
        Nome = nome.Trim();
    }
}
