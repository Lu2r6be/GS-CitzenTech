using Microsoft.EntityFrameworkCore;
using Termometro.Domain.Entidades;
using Termometro.Domain.Niveis;
using static Termometro.Domain.Niveis.NivelTermico;

namespace Termometro.Infrastructure.Tests.Persistencia;

public class PersistenciaTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private static Residente NovoResidente(string cpf = "529.982.247-25") =>
        new("Maria da Silva", Cpf.Criar(cpf), new DateOnly(1940, 6, 15), true, false);

    /// <summary>Grava microárea + residente + casa e devolve a casa (já gravada).</summary>
    private static async Task<Domicilio> GravarCasaAsync(BancoDeTeste banco, string cpf = "529.982.247-25")
    {
        await using var ctx = banco.NovoContexto();
        var micro = new Microarea($"Microárea {Guid.NewGuid():N}");
        var residente = NovoResidente(cpf);
        var casa = new Domicilio(residente.Id, micro.Id, "Rua das Flores, 10", "01000-000",
            TipoCobertura.Fibrocimento, 3);
        ctx.AddRange(micro, residente, casa);
        await ctx.SaveChangesAsync();
        return casa;
    }

    [Fact]
    public async Task Residente_GravaSoOsDigitosDoCpf_ELeDeVoltaComoObjeto()
    {
        using var banco = new BancoDeTeste();
        var casa = await GravarCasaAsync(banco);

        Assert.Equal("52998224725", banco.LerColuna<string>("SELECT Cpf FROM Residentes"));

        await using var ctx = banco.NovoContexto();
        var lido = await ctx.Residentes.SingleAsync(r => r.Id == casa.ResidenteId);
        Assert.Equal("***.982.247-**", lido.Cpf.ToString());
        Assert.Equal("52998224725", lido.Cpf.Numeros);
        Assert.Equal(new DateOnly(1940, 6, 15), lido.DataNascimento);
    }

    [Fact]
    public async Task Residente_BuscaPorCpf_FuncionaPeloValorConvertido()
    {
        using var banco = new BancoDeTeste();
        await GravarCasaAsync(banco);

        await using var ctx = banco.NovoContexto();
        var cpf = Cpf.Criar("52998224725");
        var achou = await ctx.Residentes.AnyAsync(r => r.Cpf == cpf);

        Assert.True(achou);
    }

    [Fact]
    public async Task Residente_CpfRepetido_ERejeitadoPeloBanco()
    {
        using var banco = new BancoDeTeste();
        await GravarCasaAsync(banco);

        await using var ctx = banco.NovoContexto();
        ctx.Residentes.Add(NovoResidente());

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Domicilio_GuardaOEstadoDoSemaforoEntreRequisicoes()
    {
        using var banco = new BancoDeTeste();
        var casa = await GravarCasaAsync(banco);

        // 1ª leitura (requisição 1): adota o verde.
        await using (var ctx = banco.NovoContexto())
        {
            var d = await ctx.Domicilios.SingleAsync(x => x.Id == casa.Id);
            d.RegistrarLeitura(Verde, T0);
            await ctx.SaveChangesAsync();
        }
        // 2ª e 3ª leituras (requisições 2 e 3): vermelho, ainda não confirma.
        for (var i = 1; i <= 2; i++)
        {
            await using var ctx = banco.NovoContexto();
            var d = await ctx.Domicilios.SingleAsync(x => x.Id == casa.Id);
            Assert.Equal(Verde, d.RegistrarLeitura(Vermelho, T0.AddMinutes(5 * i)));
            await ctx.SaveChangesAsync();
        }
        // 4ª leitura (requisição 4): é a terceira seguida em vermelho, agora confirma.
        await using (var ctx = banco.NovoContexto())
        {
            var d = await ctx.Domicilios.SingleAsync(x => x.Id == casa.Id);
            Assert.Equal(Vermelho, d.RegistrarLeitura(Vermelho, T0.AddMinutes(15)));
            await ctx.SaveChangesAsync();
        }

        await using var final = banco.NovoContexto();
        var lida = await final.Domicilios.SingleAsync(x => x.Id == casa.Id);
        Assert.Equal(Vermelho, lida.NivelConfirmado);
        Assert.Null(lida.NivelCandidato);
        Assert.Equal(T0.AddMinutes(15), lida.UltimaLeituraEm);
        Assert.Equal("Vermelho", banco.LerColuna<string>("SELECT NivelConfirmado FROM Domicilios"));
    }

    [Fact]
    public async Task Leitura_UltimasDoSensor_VemEmOrdemDaMaisRecenteParaAMaisAntiga()
    {
        using var banco = new BancoDeTeste();
        var casa = await GravarCasaAsync(banco);
        await using (var ctx = banco.NovoContexto())
        {
            ctx.Dispositivos.Add(new Dispositivo("esp32-001", casa.Id, "hash"));
            ctx.Leituras.AddRange(
                new Leitura("esp32-001", T0.AddMinutes(10), 31, 50, 31.5, Verde),
                new Leitura("esp32-001", T0, 30, 50, 31.0, Verde),
                new Leitura("esp32-001", T0.AddMinutes(20), 33, 60, 39.5, Vermelho));
            await ctx.SaveChangesAsync();
        }

        await using var leitura = banco.NovoContexto();
        var ultimas = await leitura.Leituras
            .Where(l => l.DispositivoId == "esp32-001")
            .OrderByDescending(l => l.RecebidaEm)
            .Take(2)
            .ToListAsync();

        Assert.Equal([T0.AddMinutes(20), T0.AddMinutes(10)], ultimas.Select(l => l.RecebidaEm));
        Assert.Equal(Vermelho, ultimas[0].Nivel);
    }

    [Fact]
    public async Task Leitura_DeDispositivoQueNaoExiste_ERejeitadaPelaChaveEstrangeira()
    {
        using var banco = new BancoDeTeste();
        await using var ctx = banco.NovoContexto();
        ctx.Leituras.Add(new Leitura("fantasma", T0, 30, 50, 31, Verde));

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Visita_MesmoIdDuasVezes_NaoDuplica()
    {
        using var banco = new BancoDeTeste();
        var casa = await GravarCasaAsync(banco);
        var acs = Usuario.CriarAcs("Ana", "ana@exemplo.com", "hash", casa.MicroareaId);
        var idDoApp = Guid.NewGuid();
        await using (var ctx = banco.NovoContexto())
        {
            ctx.Usuarios.Add(acs);
            ctx.Visitas.Add(new Visita(idDoApp, casa.Id, acs.Id, T0, OrigemVisita.Offline));
            await ctx.SaveChangesAsync();
        }

        await using var reenvio = banco.NovoContexto();
        reenvio.Visitas.Add(new Visita(idDoApp, casa.Id, acs.Id, T0, OrigemVisita.Offline));

        await Assert.ThrowsAsync<DbUpdateException>(() => reenvio.SaveChangesAsync());
    }

    [Fact]
    public async Task Visita_RealizadaGravaDatasOpcionaisEResultado()
    {
        using var banco = new BancoDeTeste();
        var casa = await GravarCasaAsync(banco);
        var acs = Usuario.CriarAcs("Ana", "ana@exemplo.com", "hash", casa.MicroareaId);
        var visita = new Visita(Guid.NewGuid(), casa.Id, acs.Id, T0);
        visita.Realizar(T0.AddHours(1), ResultadoVisita.Orientado, "Orientada a beber água");
        await using (var ctx = banco.NovoContexto())
        {
            ctx.Usuarios.Add(acs);
            ctx.Visitas.Add(visita);
            await ctx.SaveChangesAsync();
        }

        await using var leitura = banco.NovoContexto();
        var lida = await leitura.Visitas.SingleAsync();

        Assert.Equal(T0.AddHours(1), lida.RealizadaEm);
        Assert.Null(lida.SincronizadaEm);
        Assert.Equal(ResultadoVisita.Orientado, lida.Resultado);
    }

    [Fact]
    public async Task Usuario_EmailRepetido_ERejeitado()
    {
        using var banco = new BancoDeTeste();
        var casa = await GravarCasaAsync(banco);
        await using (var ctx = banco.NovoContexto())
        {
            ctx.Usuarios.Add(Usuario.CriarAcs("Ana", "ana@exemplo.com", "hash", casa.MicroareaId));
            await ctx.SaveChangesAsync();
        }

        await using var outro = banco.NovoContexto();
        outro.Usuarios.Add(Usuario.CriarAcs("Outra Ana", "ANA@exemplo.com", "hash2", casa.MicroareaId));

        await Assert.ThrowsAsync<DbUpdateException>(() => outro.SaveChangesAsync());
    }

    [Fact]
    public async Task Consentimento_DeResidenteInexistente_ERejeitado()
    {
        using var banco = new BancoDeTeste();
        await using var ctx = banco.NovoContexto();
        ctx.Consentimentos.Add(new Consentimento(Guid.NewGuid(), "Monitorar a casa", T0));

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Consentimento_RevogadoNaoApareceComoAtivo()
    {
        using var banco = new BancoDeTeste();
        var casa = await GravarCasaAsync(banco);
        var consentimento = new Consentimento(casa.ResidenteId, "Monitorar a casa", T0);
        consentimento.Revogar(T0.AddDays(1));
        await using (var ctx = banco.NovoContexto())
        {
            ctx.Consentimentos.Add(consentimento);
            await ctx.SaveChangesAsync();
        }

        await using var leitura = banco.NovoContexto();
        var ativos = await leitura.Consentimentos.Where(c => c.RevogadoEm == null).CountAsync();

        Assert.Equal(0, ativos);
    }

    [Fact]
    public async Task Domicilio_ResidenteInexistente_ERejeitado()
    {
        using var banco = new BancoDeTeste();
        await using var ctx = banco.NovoContexto();
        var micro = new Microarea("Centro");
        ctx.AddRange(micro, new Domicilio(Guid.NewGuid(), micro.Id, "Rua A", "01000-000", TipoCobertura.Laje));

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Limiar_GravaAsFaixasEOBancoCarimbaADataDeCriacao()
    {
        var agora = new DateTimeOffset(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);
        using var banco = new BancoDeTeste(new RelogioFixo(agora));
        await using (var ctx = banco.NovoContexto())
        {
            ctx.Limiares.Add(new LimiarTemperatura(13, 36, 40, "ml-2026-11-13", OrigemLimiar.ML));
            await ctx.SaveChangesAsync();
        }

        await using var leitura = banco.NovoContexto();
        var lido = await leitura.Limiares.SingleAsync();

        Assert.Equal(13, lido.AzulAbaixoDe);
        Assert.Equal(36, lido.VerdeAte);
        Assert.Equal(40, lido.AmareloAte);
        Assert.Equal(OrigemLimiar.ML, lido.Origem);
        Assert.Equal(agora, lido.CriadoEm);
    }

    [Fact]
    public async Task DiaRisco_MesmoDiaEEstacao_NaoRepete()
    {
        using var banco = new BancoDeTeste();
        await using (var ctx = banco.NovoContexto())
        {
            ctx.DiasRisco.Add(new DiaRisco(new DateOnly(2026, 11, 13), "A701", 1.08, 1.05, TipoDiaRisco.Calor, "rf-1"));
            await ctx.SaveChangesAsync();
        }

        await using var outro = banco.NovoContexto();
        outro.DiasRisco.Add(new DiaRisco(new DateOnly(2026, 11, 13), "A701", 1.09, 1.05, TipoDiaRisco.Calor, "rf-2"));

        await Assert.ThrowsAsync<DbUpdateException>(() => outro.SaveChangesAsync());
    }

    [Fact]
    public async Task LinqJoin_CasasDaMicroareaComONomeDoResidente()
    {
        using var banco = new BancoDeTeste();
        var casa = await GravarCasaAsync(banco);

        await using var ctx = banco.NovoContexto();
        var lista = await (from d in ctx.Domicilios
                           join r in ctx.Residentes on d.ResidenteId equals r.Id
                           where d.MicroareaId == casa.MicroareaId
                           select new { d.Endereco, r.Nome }).ToListAsync();

        var unica = Assert.Single(lista);
        Assert.Equal("Maria da Silva", unica.Nome);
        Assert.Equal("Rua das Flores, 10", unica.Endereco);
    }
}
