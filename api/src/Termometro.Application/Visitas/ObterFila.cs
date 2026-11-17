using Termometro.Application.Abstracoes;
using Termometro.Application.Erros;
using Termometro.Application.Risco;
using Termometro.Domain.Entidades;
using Termometro.Domain.Fila;
using Termometro.Domain.Niveis;

namespace Termometro.Application.Visitas;

public sealed record CasaNaFila(
    Guid DomicilioId,
    string Endereco,
    string NomeResidente,
    NivelTermico Nivel,
    bool SemSinal,
    double? TemperaturaC,
    DateTimeOffset? UltimaLeituraEm,
    int PontosDeVulnerabilidade,
    DateTimeOffset? UltimaVisitaEm);

public sealed record FilaDeVisitas(TipoDiaRisco DiaDeRisco, IReadOnlyList<CasaNaFila> Casas);

/// <summary>A fila do ACS: só a microárea dele, mais urgente primeiro (regra em OrdenadorDeFila).</summary>
public sealed class ObterFila(
    IMonitoramentoRepositorio monitoramento,
    IOrdenadorDeFila ordenador,
    ServicoDiaDeRisco diaDeRisco)
{
    public async Task<FilaDeVisitas> ExecutarAsync(UsuarioAutenticado usuario, CancellationToken ct)
    {
        Permissoes.ExigirPapel(usuario, PapelUsuario.Acs);
        var microareaId = usuario.MicroareaId
            ?? throw new AcessoNegado("O ACS não está ligado a uma microárea.");

        var agora = diaDeRisco.Agora;
        var hoje = DateOnly.FromDateTime(agora.UtcDateTime);
        var dia = await diaDeRisco.DeHojeAsync(ct);
        var casas = await monitoramento.ListarCasasDaMicroareaAsync(microareaId, ct);

        var porId = casas.ToDictionary(c => c.Domicilio.Id);
        var itens = casas.Select(c => new ItemFila(
            c.Domicilio.Id,
            c.Domicilio.NivelExibido(agora),
            c.Residente.VulnerabilidadeEm(hoje),
            c.UltimaVisitaRealizadaEm is { } ultima ? agora - ultima : null));

        var ordenadas = ordenador.Ordenar(itens, dia)
            .Select(item => ParaLinhaDaFila(porId[item.DomicilioId], item))
            .ToList();

        return new FilaDeVisitas(dia, ordenadas);
    }

    private static CasaNaFila ParaLinhaDaFila(CasaMonitorada casa, ItemFila item) => new(
        casa.Domicilio.Id,
        casa.Domicilio.Endereco,
        casa.Residente.Nome,
        item.Nivel,
        item.Nivel == NivelTermico.Cinza,
        casa.UltimaLeitura?.TemperaturaC,
        casa.Domicilio.UltimaLeituraEm,
        item.Vulnerabilidade.Pontos,
        casa.UltimaVisitaRealizadaEm);
}
