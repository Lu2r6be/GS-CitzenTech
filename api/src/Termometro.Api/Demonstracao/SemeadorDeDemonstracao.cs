using Microsoft.EntityFrameworkCore;
using Termometro.Application.Abstracoes;
using Termometro.Domain.Entidades;
using Termometro.Domain.Niveis;
using Termometro.Infrastructure.Persistencia;

namespace Termometro.Api.Demonstracao;

public sealed class OpcoesDeDemonstracao
{
    public const string Secao = "Demonstracao";

    public bool Habilitada { get; set; }
    public string SenhaDosUsuarios { get; set; } = string.Empty;
    public string ChaveDoDispositivo { get; set; } = string.Empty;
}

/// <summary>
/// Cria o cenário do pitch (contrato §10b): uma microárea, uma ACS, 5 casas e a avó com o ESP32 real.
/// Todos os dados são FICTÍCIOS e só existem para a demo. Só roda se Demonstracao:Habilitada = true,
/// e nunca mexe em um banco que já tem dados.
/// </summary>
public sealed class SemeadorDeDemonstracao(TermometroDbContext banco, IHashDeSenha hashDeSenha,
    IHashDeChaveDeDispositivo hashDeChave, TimeProvider relogio, ILogger<SemeadorDeDemonstracao> log)
{
    public const string EmailAdmin = "admin@demo.local";
    public const string EmailAcs = "ana.acs@demo.local";
    public const string EmailFamiliar = "neto@demo.local";
    public const string DispositivoDaAvo = "esp32-001";

    public async Task SemearAsync(OpcoesDeDemonstracao opcoes, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(opcoes.SenhaDosUsuarios) || string.IsNullOrEmpty(opcoes.ChaveDoDispositivo))
        {
            throw new InvalidOperationException(
                "Demonstração habilitada, mas faltam Demonstracao:SenhaDosUsuarios e Demonstracao:ChaveDoDispositivo.");
        }
        if (await banco.Microareas.AnyAsync(ct))
        {
            log.LogInformation("Banco já tem dados: demonstração não foi recriada.");
            return;
        }

        var agora = relogio.GetUtcNow();
        var microarea = new Microarea("Microárea 01 (demonstração)");
        banco.Microareas.Add(microarea);

        var moradores = new (string Nome, string Cpf, int AnoNascimento, bool Sozinho, bool Cronica,
            string Endereco, string Cep, TipoCobertura Cobertura, string DispositivoId)[]
        {
            ("Maria das Dores (a avó)", "52998224725", 1938, true, true, "Rua das Acácias, 120", "01310-100", TipoCobertura.Fibrocimento, DispositivoDaAvo),
            ("José Almeida", "11144477735", 1944, false, true, "Rua do Sol, 45", "01310-200", TipoCobertura.Laje, "sim-002"),
            ("Antônia Souza", "39053344705", 1936, true, false, "Av. Brasil, 900, ap. 31", "01310-300", TipoCobertura.Telha, "sim-003"),
            ("Benedito Lima", "16899535009", 1951, false, false, "Rua Verde, 7", "01310-400", TipoCobertura.Laje, "sim-004"),
            ("Célia Ramos", "93541134780", 1948, true, false, "Travessa da Paz, 18", "01310-500", TipoCobertura.Fibrocimento, "sim-005"),
        };

        Residente? avo = null;
        foreach (var m in moradores)
        {
            var residente = new Residente(m.Nome, Cpf.Criar(m.Cpf), new DateOnly(m.AnoNascimento, 6, 15),
                m.Sozinho, m.Cronica);
            var casa = new Domicilio(residente.Id, microarea.Id, m.Endereco, m.Cep, m.Cobertura);
            banco.AddRange(residente, casa,
                new Consentimento(residente.Id, "Monitorar a temperatura da casa para prevenir riscos à saúde", agora),
                new Dispositivo(m.DispositivoId, casa.Id, hashDeChave.Gerar(opcoes.ChaveDoDispositivo)));
            avo ??= residente;
        }

        var hashDaSenha = hashDeSenha.Gerar(opcoes.SenhaDosUsuarios);
        banco.AddRange(
            Usuario.CriarAdmin("Administrador (demo)", EmailAdmin, hashDaSenha),
            Usuario.CriarAcs("Ana (ACS demo)", EmailAcs, hashDaSenha, microarea.Id),
            Usuario.CriarFamiliar("Neto da Maria (demo)", EmailFamiliar, hashDaSenha, avo!.Id),
            new LimiarTemperatura(
                LimiarTemperatura.Padrao.AzulAbaixoDe, LimiarTemperatura.Padrao.VerdeAte,
                LimiarTemperatura.Padrao.AmareloAte, LimiarTemperatura.Padrao.Versao, OrigemLimiar.Regra));

        await banco.SaveChangesAsync(ct);
        log.LogInformation("Cenário de demonstração criado (5 casas, 1 ACS, 1 familiar, 1 admin).");
    }
}
