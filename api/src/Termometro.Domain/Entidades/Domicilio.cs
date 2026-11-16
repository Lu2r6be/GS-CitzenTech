using Termometro.Domain.Niveis;
using Termometro.Domain.Seguranca;

namespace Termometro.Domain.Entidades;

/// <summary>
/// A casa monitorada. Guarda também o estado do semáforo (nível confirmado, candidato e contagem),
/// para a confirmação de leituras (histerese) continuar de onde parou a cada nova leitura.
/// </summary>
public sealed class Domicilio
{
    public Guid Id { get; private set; }
    public Guid ResidenteId { get; private set; }
    public Guid MicroareaId { get; private set; }

    [Pii(CategoriaPii.Endereco)]
    public string Endereco { get; private set; } = string.Empty;

    [Pii(CategoriaPii.Endereco)]
    public string Cep { get; private set; } = string.Empty;

    public TipoCobertura TipoCobertura { get; private set; }
    public int? Andar { get; private set; }

    public NivelTermico? NivelConfirmado { get; private set; }
    public NivelTermico? NivelCandidato { get; private set; }
    public int LeiturasDoCandidato { get; private set; }
    public DateTimeOffset? UltimaLeituraEm { get; private set; }

    private Domicilio() { }

    public Domicilio(Guid residenteId, Guid microareaId, string endereco, string cep,
        TipoCobertura tipoCobertura, int? andar = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endereco);
        ArgumentException.ThrowIfNullOrWhiteSpace(cep);
        if (residenteId == Guid.Empty || microareaId == Guid.Empty)
        {
            throw new ArgumentException("Residente e microárea são obrigatórios.");
        }

        Id = Guid.CreateVersion7();
        ResidenteId = residenteId;
        MicroareaId = microareaId;
        Endereco = endereco.Trim();
        Cep = cep.Trim();
        TipoCobertura = tipoCobertura;
        Andar = andar;
    }

    /// <summary>
    /// Registra o nível lido pelo sensor e devolve o nível confirmado (histerese).
    /// Leitura atrasada (mais antiga que a última já recebida) é ignorada: não pode desfazer o presente.
    /// </summary>
    public NivelTermico RegistrarLeitura(NivelTermico nivelLido, DateTimeOffset em)
    {
        if (UltimaLeituraEm is { } ultima && em <= ultima)
        {
            return NivelConfirmado ?? nivelLido;
        }

        var confirmador = new ConfirmadorDeNivel(NivelConfirmado, NivelCandidato, LeiturasDoCandidato);
        var confirmado = confirmador.Registrar(nivelLido);

        NivelConfirmado = confirmador.Atual;
        NivelCandidato = confirmador.Candidato;
        LeiturasDoCandidato = confirmador.LeiturasDoCandidato;
        UltimaLeituraEm = em;
        return confirmado;
    }

    /// <summary>O que o app mostra agora: cinza se o sensor está mudo, senão o nível confirmado.</summary>
    public NivelTermico NivelExibido(DateTimeOffset agora) =>
        StatusSinal.EstaSemSinal(UltimaLeituraEm, agora)
            ? NivelTermico.Cinza
            : NivelConfirmado ?? NivelTermico.Cinza;
}
