namespace Termometro.Domain.Niveis;

/// <summary>
/// Confirmação de leituras (histerese): evita o semáforo piscando.
/// O nível só PIORA depois de 3 leituras seguidas na faixa nova e só MELHORA depois de 6.
/// O estado (Atual, Candidato, contagem) é público para poder ser guardado no banco
/// e restaurado na próxima leitura.
/// </summary>
public sealed class ConfirmadorDeNivel
{
    public const int LeiturasParaPiorar = 3;
    public const int LeiturasParaMelhorar = 6;

    public NivelTermico? Atual { get; private set; }
    public NivelTermico? Candidato { get; private set; }
    public int LeiturasDoCandidato { get; private set; }

    public ConfirmadorDeNivel() { }

    /// <summary>Restaura um estado salvo no banco.</summary>
    public ConfirmadorDeNivel(NivelTermico? atual, NivelTermico? candidato, int leiturasDoCandidato)
    {
        if (atual == NivelTermico.Cinza || candidato == NivelTermico.Cinza)
        {
            throw new ArgumentException("Cinza (sem sinal) não é um nível de leitura.");
        }
        if (leiturasDoCandidato < 0 || (candidato is null) != (leiturasDoCandidato == 0))
        {
            throw new ArgumentException("Estado inconsistente: candidato e contagem não combinam.");
        }
        Atual = atual;
        Candidato = candidato;
        LeiturasDoCandidato = leiturasDoCandidato;
    }

    /// <summary>Registra o nível de uma nova leitura e devolve o nível confirmado.</summary>
    public NivelTermico Registrar(NivelTermico lido)
    {
        if (lido == NivelTermico.Cinza)
        {
            throw new ArgumentException("Cinza (sem sinal) não vem de uma leitura.", nameof(lido));
        }

        // Primeira leitura da casa: não há nada com que comparar.
        if (Atual is null)
        {
            Atual = lido;
            LimparCandidato();
            return lido;
        }

        if (lido == Atual)
        {
            LimparCandidato();
            return lido;
        }

        if (lido == Candidato)
        {
            LeiturasDoCandidato++;
        }
        else
        {
            Candidato = lido;
            LeiturasDoCandidato = 1;
        }

        var exigidas = lido.Gravidade() < Atual.Value.Gravidade()
            ? LeiturasParaMelhorar
            : LeiturasParaPiorar;

        if (LeiturasDoCandidato >= exigidas)
        {
            Atual = lido;
            LimparCandidato();
        }

        return Atual.Value;
    }

    private void LimparCandidato()
    {
        Candidato = null;
        LeiturasDoCandidato = 0;
    }
}
