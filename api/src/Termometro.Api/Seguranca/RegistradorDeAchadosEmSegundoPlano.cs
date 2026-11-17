using System.Threading.Channels;
using Termometro.Application.Abstracoes;
using Termometro.Application.Seguranca;
using Termometro.Domain.Entidades;

namespace Termometro.Api.Seguranca;

public sealed class OpcoesDeSeguranca
{
    public const string Secao = "Seguranca";

    /// <summary>
    /// Achados iguais (mesmo tipo, origem, saída e categoria) dentro desta janela viram um só registro.
    /// Evita que um laço com milhares de logs vazando encha o banco. 0 desliga o agrupamento (usado nos testes).
    /// </summary>
    public int JanelaDeAgrupamentoEmSegundos { get; set; } = 30;
}

/// <summary>
/// Recebe achados sem atrasar quem os gerou (o logger e a resposta HTTP não esperam o banco)
/// e grava em segundo plano. A fila tem tamanho fixo: se encher, descarta o mais antigo em vez de travar a API.
/// </summary>
public sealed class RegistradorDeAchadosEmSegundoPlano(
    IServiceScopeFactory escopos,
    TimeProvider relogio,
    Microsoft.Extensions.Options.IOptions<OpcoesDeSeguranca> opcoes,
    ILogger<RegistradorDeAchadosEmSegundoPlano> log) : BackgroundService, IRegistradorDeAchados
{
    private readonly Channel<NovoAchado> _fila = Channel.CreateBounded<NovoAchado>(
        new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private readonly Dictionary<string, DateTimeOffset> _ultimoRegistro = [];
    private readonly object _trava = new();

    public void Registrar(NovoAchado achado)
    {
        if (JaRegistradoRecentemente(achado))
        {
            return;
        }
        _fila.Writer.TryWrite(achado);
    }

    private bool JaRegistradoRecentemente(NovoAchado a)
    {
        var janela = TimeSpan.FromSeconds(opcoes.Value.JanelaDeAgrupamentoEmSegundos);
        if (janela <= TimeSpan.Zero)
        {
            return false;
        }

        var agora = relogio.GetUtcNow();
        var chave = $"{a.Tipo}|{a.Origem}|{a.Exposicao}|{a.Categoria}";
        lock (_trava)
        {
            if (_ultimoRegistro.TryGetValue(chave, out var ultimo) && agora - ultimo < janela)
            {
                return true;
            }
            _ultimoRegistro[chave] = agora;
            if (_ultimoRegistro.Count > 1000)
            {
                foreach (var velha in _ultimoRegistro.Where(p => agora - p.Value >= janela).Select(p => p.Key).ToList())
                {
                    _ultimoRegistro.Remove(velha);
                }
            }
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        var lote = new List<NovoAchado>();
        try
        {
            while (await _fila.Reader.WaitToReadAsync(parada))
            {
                lote.Clear();
                while (lote.Count < 100 && _fila.Reader.TryRead(out var item))
                {
                    lote.Add(item);
                }
                await GravarAsync(lote, parada);
            }
        }
        catch (OperationCanceledException)
        {
            // desligando a API: o que ficou na fila é descartado de propósito (são só alertas)
        }
    }

    private async Task GravarAsync(List<NovoAchado> lote, CancellationToken ct)
    {
        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            var repositorio = escopo.ServiceProvider.GetRequiredService<IAchadosRepositorio>();
            var unidade = escopo.ServiceProvider.GetRequiredService<IUnidadeDeTrabalho>();
            var agora = relogio.GetUtcNow();
            var entidades = lote.Select(a => new AchadoSeguranca(a.Tipo, a.Severidade, a.Origem, a.Exposicao, a.Categoria, agora)).ToList();
            await repositorio.AdicionarAsync(entidades, ct);
            await unidade.SalvarAsync(ct);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            log.LogError("Não consegui gravar {Quantidade} achado(s) de segurança ({TipoDoErro}).", lote.Count, erro.GetType().Name);
        }
    }
}
