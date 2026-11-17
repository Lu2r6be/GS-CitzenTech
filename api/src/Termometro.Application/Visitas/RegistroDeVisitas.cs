using Termometro.Application.Abstracoes;
using Termometro.Application.Erros;
using Termometro.Application.Risco;
using Termometro.Domain.Entidades;

namespace Termometro.Application.Visitas;

// ---------- Visita feita no app, com internet ----------

public sealed record RealizarVisitaPedido(Guid DomicilioId, ResultadoVisita Resultado, string? Observacao);

public sealed record VisitaRegistrada(Guid Id, Guid DomicilioId, DateTimeOffset RealizadaEm,
    ResultadoVisita Resultado);

/// <summary>
/// O app gera o GUID da visita. Repetir o mesmo pedido (rede ruim, usuário clicou 2x) não duplica:
/// devolve a visita que já existe.
/// </summary>
public sealed class RealizarVisita(
    IMonitoramentoRepositorio monitoramento,
    IVisitasRepositorio visitas,
    IUnidadeDeTrabalho unidade,
    ServicoDiaDeRisco relogio)
{
    public async Task<VisitaRegistrada> ExecutarAsync(UsuarioAutenticado acs, Guid visitaId,
        RealizarVisitaPedido pedido, CancellationToken ct)
    {
        Permissoes.ExigirPapel(acs, PapelUsuario.Acs);
        if (visitaId == Guid.Empty)
        {
            throw new DadosInvalidos("O id da visita é obrigatório.");
        }

        var casa = await monitoramento.ObterDomicilioAsync(pedido.DomicilioId, ct)
            ?? throw new NaoEncontrado("Casa não encontrada.");
        Permissoes.ExigirAcessoACasa(acs, casa);

        var agora = relogio.Agora;
        var visita = await visitas.ObterAsync(visitaId, ct);

        if (visita is { RealizadaEm: not null })
        {
            return Resumir(visita, acs);
        }

        try
        {
            if (visita is null)
            {
                visita = new Visita(visitaId, casa.Id, acs.Id, agora, OrigemVisita.Online);
                await visitas.AdicionarAsync(visita, ct);
            }
            ExigirMesmaVisita(visita, casa, acs);
            visita.Realizar(agora, pedido.Resultado, pedido.Observacao);
        }
        catch (ArgumentException erro)
        {
            throw new DadosInvalidos(erro.Message);
        }

        await unidade.SalvarAsync(ct);
        return Resumir(visita, acs);
    }

    private static VisitaRegistrada Resumir(Visita v, UsuarioAutenticado acs)
    {
        if (v.AcsId != acs.Id)
        {
            throw new AcessoNegado("Esta visita pertence a outro ACS.");
        }
        return new VisitaRegistrada(v.Id, v.DomicilioId, v.RealizadaEm!.Value, v.Resultado);
    }

    private static void ExigirMesmaVisita(Visita v, Domicilio casa, UsuarioAutenticado acs)
    {
        if (v.AcsId != acs.Id || v.DomicilioId != casa.Id)
        {
            throw new Conflito("Este id de visita já foi usado em outra casa ou por outro ACS.");
        }
    }
}

// ---------- Lote de visitas feitas offline ----------

public sealed record VisitaOffline(Guid Id, Guid DomicilioId, DateTimeOffset? PlanejadaEm,
    DateTimeOffset RealizadaEm, ResultadoVisita Resultado, string? Observacao);

public enum StatusDaSincronizacao { Criada, JaExistia, Rejeitada }

public sealed record ResultadoDaVisita(Guid Id, StatusDaSincronizacao Status, string? Motivo);

public sealed record ResultadoDaSincronizacao(IReadOnlyList<ResultadoDaVisita> Visitas);

/// <summary>
/// Recebe as visitas que o ACS fez sem internet. Idempotente pelo GUID: mandar o lote de novo
/// (o app não sabe se a primeira tentativa chegou) não cria nada duplicado. Um item ruim não derruba o lote.
/// </summary>
public sealed class SincronizarVisitas(
    IMonitoramentoRepositorio monitoramento,
    IVisitasRepositorio visitas,
    IUnidadeDeTrabalho unidade,
    ServicoDiaDeRisco relogio)
{
    public const int TamanhoMaximoDoLote = 200;
    private static readonly TimeSpan ToleranciaDeRelogio = TimeSpan.FromMinutes(5);

    public async Task<ResultadoDaSincronizacao> ExecutarAsync(UsuarioAutenticado acs,
        IReadOnlyList<VisitaOffline> lote, CancellationToken ct)
    {
        Permissoes.ExigirPapel(acs, PapelUsuario.Acs);
        if (lote.Count > TamanhoMaximoDoLote)
        {
            throw new DadosInvalidos($"O lote aceita no máximo {TamanhoMaximoDoLote} visitas.");
        }

        var agora = relogio.Agora;
        var vistosNoLote = new HashSet<Guid>();
        var resultados = new List<ResultadoDaVisita>(lote.Count);

        foreach (var item in lote)
        {
            resultados.Add(await ProcessarAsync(acs, item, agora, vistosNoLote, ct));
        }

        await unidade.SalvarAsync(ct);
        return new ResultadoDaSincronizacao(resultados);
    }

    private async Task<ResultadoDaVisita> ProcessarAsync(UsuarioAutenticado acs, VisitaOffline item,
        DateTimeOffset agora, HashSet<Guid> vistosNoLote, CancellationToken ct)
    {
        if (!vistosNoLote.Add(item.Id))
        {
            return new(item.Id, StatusDaSincronizacao.JaExistia, "Repetida dentro do mesmo lote.");
        }
        if (item.RealizadaEm > agora + ToleranciaDeRelogio)
        {
            return Rejeitada(item.Id, "A data da visita está no futuro (relógio do celular errado?).");
        }

        var existente = await visitas.ObterAsync(item.Id, ct);
        if (existente is not null)
        {
            return existente.AcsId == acs.Id
                ? new(item.Id, StatusDaSincronizacao.JaExistia, null)
                : Rejeitada(item.Id, "Id já usado por outro ACS.");
        }

        var casa = await monitoramento.ObterDomicilioAsync(item.DomicilioId, ct);
        if (casa is null || casa.MicroareaId != acs.MicroareaId)
        {
            // Mesma resposta para "não existe" e "é de outra microárea": não revela casas alheias.
            return Rejeitada(item.Id, "Casa não encontrada na sua microárea.");
        }

        try
        {
            var visita = new Visita(item.Id, casa.Id, acs.Id, item.PlanejadaEm ?? item.RealizadaEm,
                OrigemVisita.Offline);
            visita.Realizar(item.RealizadaEm, item.Resultado, item.Observacao);
            visita.MarcarSincronizada(agora);
            await visitas.AdicionarAsync(visita, ct);
        }
        catch (ArgumentException erro)
        {
            return Rejeitada(item.Id, erro.Message);
        }

        return new(item.Id, StatusDaSincronizacao.Criada, null);
    }

    private static ResultadoDaVisita Rejeitada(Guid id, string motivo) =>
        new(id, StatusDaSincronizacao.Rejeitada, motivo);
}
