namespace Termometro.Domain.Entidades;

/// <summary>
/// O ESP32 de uma casa. O dispositivo NÃO conhece PII: só o seu código (ex.: "esp32-001") e um segredo.
/// A ligação dispositivo → casa existe apenas no banco. Só o HASH do segredo é guardado.
/// </summary>
public sealed class Dispositivo
{
    /// <summary>Código que o ESP32 envia no JSON (campo deviceId).</summary>
    public string Id { get; private set; } = string.Empty;

    public Guid DomicilioId { get; private set; }
    public string DeviceKeyHash { get; private set; } = string.Empty;
    public bool Ativo { get; private set; }

    private Dispositivo() { }

    public Dispositivo(string id, Guid domicilioId, string deviceKeyHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceKeyHash);
        if (domicilioId == Guid.Empty)
        {
            throw new ArgumentException("Domicílio obrigatório.", nameof(domicilioId));
        }

        Id = id.Trim();
        DomicilioId = domicilioId;
        DeviceKeyHash = deviceKeyHash;
        Ativo = true;
    }

    public void Desativar() => Ativo = false;
}
