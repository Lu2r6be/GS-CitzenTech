using Termometro.Domain.Niveis;

namespace Termometro.Domain.Entidades;

/// <summary>Resultado do ML para um dia: risco relativo previsto e se é dia de calor ou de frio perigoso.</summary>
public sealed class DiaRisco
{
    public int Id { get; private set; }
    public DateOnly Data { get; private set; }
    public string Estacao { get; private set; } = string.Empty;
    public double RrPrevisto { get; private set; }
    public double Corte { get; private set; }
    public TipoDiaRisco Tipo { get; private set; }
    public string ModeloVersao { get; private set; } = string.Empty;

    private DiaRisco() { }

    public DiaRisco(DateOnly data, string estacao, double rrPrevisto, double corte,
        TipoDiaRisco tipo, string modeloVersao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(estacao);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeloVersao);

        Data = data;
        Estacao = estacao;
        RrPrevisto = rrPrevisto;
        Corte = corte;
        Tipo = tipo;
        ModeloVersao = modeloVersao;
    }
}
