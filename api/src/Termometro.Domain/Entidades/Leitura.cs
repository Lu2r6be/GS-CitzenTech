using Termometro.Domain.Niveis;

namespace Termometro.Domain.Entidades;

/// <summary>Uma medição do sensor. A hora é a do RECEBIMENTO na API (o ESP32 não precisa de relógio).</summary>
public sealed class Leitura
{
    public long Id { get; private set; }
    public string DispositivoId { get; private set; } = string.Empty;
    public DateTimeOffset RecebidaEm { get; private set; }
    public double TemperaturaC { get; private set; }
    public double Umidade { get; private set; }
    public double IndiceCalorC { get; private set; }

    /// <summary>Nível desta leitura sozinha, ANTES da confirmação (histerese).</summary>
    public NivelTermico Nivel { get; private set; }

    private Leitura() { }

    public Leitura(string dispositivoId, DateTimeOffset recebidaEm, double temperaturaC,
        double umidade, double indiceCalorC, NivelTermico nivel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dispositivoId);
        if (nivel == NivelTermico.Cinza)
        {
            throw new ArgumentException("Uma leitura nunca é cinza (sem sinal).", nameof(nivel));
        }

        DispositivoId = dispositivoId;
        RecebidaEm = recebidaEm;
        TemperaturaC = temperaturaC;
        Umidade = umidade;
        IndiceCalorC = indiceCalorC;
        Nivel = nivel;
    }
}
