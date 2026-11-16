namespace Termometro.Domain.Entidades;

/// <summary>Autorização (LGPD) do residente para o monitoramento. Sem consentimento ativo, não há cadastro.</summary>
public sealed class Consentimento
{
    public Guid Id { get; private set; }
    public Guid ResidenteId { get; private set; }
    public string Finalidade { get; private set; } = string.Empty;
    public DateTimeOffset ConcedidoEm { get; private set; }
    public DateTimeOffset? RevogadoEm { get; private set; }

    public bool EstaAtivo => RevogadoEm is null;

    private Consentimento() { }

    public Consentimento(Guid residenteId, string finalidade, DateTimeOffset concedidoEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalidade);
        if (residenteId == Guid.Empty)
        {
            throw new ArgumentException("Residente obrigatório.", nameof(residenteId));
        }

        Id = Guid.CreateVersion7();
        ResidenteId = residenteId;
        Finalidade = finalidade.Trim();
        ConcedidoEm = concedidoEm;
    }

    public void Revogar(DateTimeOffset em)
    {
        if (!EstaAtivo)
        {
            throw new InvalidOperationException("O consentimento já foi revogado.");
        }
        if (em < ConcedidoEm)
        {
            throw new ArgumentException("A revogação não pode ser anterior à concessão.", nameof(em));
        }
        RevogadoEm = em;
    }
}
