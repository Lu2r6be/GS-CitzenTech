using Termometro.Application.Abstracoes;
using Termometro.Application.Erros;
using Termometro.Application.Risco;
using Termometro.Domain.Entidades;
using Termometro.Domain.Niveis;

namespace Termometro.Application.Domicilios;

public sealed record LeituraResumo(DateTimeOffset RecebidaEm, double TemperaturaC, double Umidade,
    double IndiceCalorC, NivelTermico Nivel);

public sealed record DetalheDaCasa(
    Guid Id,
    string Endereco,
    string NomeResidente,
    string CpfMascarado,
    TipoCobertura TipoCobertura,
    NivelTermico Nivel,
    bool SemSinal,
    DateTimeOffset? UltimaLeituraEm,
    SentidoTendencia Tendencia,
    IReadOnlyList<LeituraResumo> UltimasLeituras);

public sealed record CasaDoFamiliar(NivelTermico Nivel, bool SemSinal, string Mensagem,
    double? TemperaturaC, DateTimeOffset? UltimaLeituraEm);

/// <summary>Consultas de casa: resumo com últimas leituras, histórico e a visão simples do familiar.</summary>
public sealed class ConsultarCasas(IMonitoramentoRepositorio monitoramento, ServicoDiaDeRisco relogio)
{
    private const int LeiturasNoResumo = 20;
    private const int LimiteDoHistorico = 1000;

    public async Task<DetalheDaCasa> DetalharAsync(UsuarioAutenticado usuario, Guid domicilioId, CancellationToken ct)
    {
        var casa = await CarregarComPermissaoAsync(usuario, domicilioId, ct);
        var residente = await monitoramento.ObterResidenteAsync(casa.ResidenteId, ct)
            ?? throw new NaoEncontrado("Residente não encontrado.");

        var agora = relogio.Agora;
        var leituras = await monitoramento.ListarLeiturasAsync(domicilioId, null, null, LeiturasNoResumo, ct);
        var tendencia = Tendencia.Calcular(leituras.Select(l => (l.RecebidaEm, l.TemperaturaC)), agora);
        var nivel = casa.NivelExibido(agora);

        return new DetalheDaCasa(
            casa.Id, casa.Endereco, residente.Nome,
            residente.Cpf.Mascarado, // nunca o CPF completo
            casa.TipoCobertura, nivel, nivel == NivelTermico.Cinza, casa.UltimaLeituraEm, tendencia,
            leituras.Select(ParaResumo).ToList());
    }

    public async Task<IReadOnlyList<LeituraResumo>> LeiturasAsync(UsuarioAutenticado usuario, Guid domicilioId,
        DateTimeOffset? de, DateTimeOffset? ate, CancellationToken ct)
    {
        if (de is not null && ate is not null && de > ate)
        {
            throw new DadosInvalidos("O parâmetro 'de' não pode ser depois de 'ate'.");
        }

        await CarregarComPermissaoAsync(usuario, domicilioId, ct);
        var leituras = await monitoramento.ListarLeiturasAsync(domicilioId, de, ate, LimiteDoHistorico, ct);
        return leituras.Select(ParaResumo).ToList();
    }

    public async Task<CasaDoFamiliar> DoFamiliarAsync(UsuarioAutenticado usuario, CancellationToken ct)
    {
        Permissoes.ExigirPapel(usuario, PapelUsuario.Familiar);
        var residenteId = usuario.ResidenteId ?? throw new AcessoNegado("Familiar sem residente vinculado.");
        var casa = await monitoramento.ObterDomicilioDoResidenteAsync(residenteId, ct)
            ?? throw new NaoEncontrado("Casa não encontrada.");

        var agora = relogio.Agora;
        var nivel = casa.NivelExibido(agora);
        var ultima = (await monitoramento.ListarLeiturasAsync(casa.Id, null, null, 1, ct)).FirstOrDefault();

        return new CasaDoFamiliar(nivel, nivel == NivelTermico.Cinza, MensagemSimples(nivel),
            ultima?.TemperaturaC, casa.UltimaLeituraEm);
    }

    private async Task<Domicilio> CarregarComPermissaoAsync(UsuarioAutenticado usuario, Guid id, CancellationToken ct)
    {
        var casa = await monitoramento.ObterDomicilioAsync(id, ct)
            ?? throw new NaoEncontrado("Casa não encontrada.");
        Permissoes.ExigirAcessoACasa(usuario, casa);
        return casa;
    }

    private static LeituraResumo ParaResumo(Leitura l) =>
        new(l.RecebidaEm, l.TemperaturaC, l.Umidade, l.IndiceCalorC, l.Nivel);

    /// <summary>Texto para quem não é profissional de saúde: diz o que está acontecendo e o que fazer.</summary>
    private static string MensagemSimples(NivelTermico nivel) => nivel switch
    {
        NivelTermico.Verde => "Tudo certo: a casa está numa temperatura confortável.",
        NivelTermico.Amarelo => "Atenção: a casa está quente. Vale ligar e lembrar de beber água.",
        NivelTermico.Vermelho => "Perigo: a casa está muito quente. Ligue agora ou peça uma visita.",
        NivelTermico.Azul => "Atenção: a casa está fria. Veja se há agasalho e cobertor.",
        _ => "Sem sinal do sensor há algum tempo. Vale ligar para conferir se está tudo bem.",
    };
}
