using Termometro.Domain.Seguranca;

namespace Termometro.Domain.Entidades;

/// <summary>
/// Um bloqueio ou alerta do módulo de segurança (dado sensível que quase vazou).
/// NÃO guarda o dado sensível: só o tipo, onde ia vazar e quão grave seria.
/// A combinação dos achados alimenta o indicador "Risco Cidadão".
/// </summary>
public sealed class AchadoSeguranca
{
    public int Id { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public SeveridadeAchado Severidade { get; private set; }
    public string Origem { get; private set; } = string.Empty;
    public ExposicaoAchado Exposicao { get; private set; }
    public CategoriaPii? CategoriaPii { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private AchadoSeguranca() { }

    public AchadoSeguranca(string tipo, SeveridadeAchado severidade, string origem,
        ExposicaoAchado exposicao, CategoriaPii? categoriaPii, DateTimeOffset criadoEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);
        ArgumentException.ThrowIfNullOrWhiteSpace(origem);

        Tipo = tipo;
        Severidade = severidade;
        Origem = origem;
        Exposicao = exposicao;
        CategoriaPii = categoriaPii;
        CriadoEm = criadoEm;
    }
}
