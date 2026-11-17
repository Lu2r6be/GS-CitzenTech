using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Termometro.Application.Abstracoes;
using Termometro.Application.Seguranca;
using Termometro.Infrastructure.Persistencia;
using Termometro.Infrastructure.Repositorios;
using Termometro.Infrastructure.Seguranca;

namespace Termometro.Infrastructure;

public static class InjecaoDeDependencia
{
    /// <param name="configurarBanco">Quem chama escolhe o banco (PostgreSQL na VM, SQLite no PC e nos testes).</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection servicos,
        Action<DbContextOptionsBuilder> configurarBanco)
    {
        servicos.AddDbContext<TermometroDbContext>(configurarBanco);

        servicos.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        servicos.AddScoped<IMonitoramentoRepositorio, MonitoramentoRepositorio>();
        servicos.AddScoped<IVisitasRepositorio, VisitasRepositorio>();
        servicos.AddScoped<ICadastroRepositorio, CadastroRepositorio>();
        servicos.AddScoped<IAchadosRepositorio, AchadosRepositorio>();

        servicos.AddSingleton<IHashDeSenha, HashDeSenhaPbkdf2>();
        servicos.AddSingleton<IHashDeChaveDeDispositivo, HashDeChaveDeDispositivoSha256>();
        return servicos;
    }
}
