namespace Termometro.Domain.Entidades;

/// <summary>Observação do clima de FORA (INMET) usada para decidir se hoje é dia de risco.</summary>
public sealed class ClimaExterno
{
    public int Id { get; private set; }
    public string Estacao { get; private set; } = string.Empty;
    public DateTimeOffset ObservadoEm { get; private set; }
    public double TempMaxC { get; private set; }
    public double TempMinC { get; private set; }
    public double Umidade { get; private set; }

    private ClimaExterno() { }

    public ClimaExterno(string estacao, DateTimeOffset observadoEm, double tempMaxC,
        double tempMinC, double umidade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(estacao);
        if (tempMinC > tempMaxC)
        {
            throw new ArgumentException("A mínima não pode ser maior que a máxima.", nameof(tempMinC));
        }

        Estacao = estacao;
        ObservadoEm = observadoEm;
        TempMaxC = tempMaxC;
        TempMinC = tempMinC;
        Umidade = umidade;
    }
}
