using Microsoft.Extensions.DependencyInjection;
using Termometro.Application.Autenticacao;
using Termometro.Application.Cadastro;
using Termometro.Application.Config;
using Termometro.Application.Domicilios;
using Termometro.Application.Risco;
using Termometro.Application.Seguranca;
using Termometro.Application.Telemetria;
using Termometro.Application.Visitas;
using Termometro.Domain.Calor;
using Termometro.Domain.Fila;

namespace Termometro.Application;

public static class InjecaoDeDependencia
{
    /// <summary>Registra as regras do Domain e os casos de uso. Uma instância por requisição (Scoped).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection servicos)
    {
        servicos.AddSingleton(TimeProvider.System);

        servicos.AddSingleton<IClassificadorNivel, ClassificadorNivel>();
        servicos.AddSingleton<IOrdenadorDeFila, OrdenadorDeFila>();

        servicos.AddScoped<ServicoDiaDeRisco>();
        servicos.AddScoped<ReceberTelemetria>();
        servicos.AddScoped<ObterFila>();
        servicos.AddScoped<RealizarVisita>();
        servicos.AddScoped<SincronizarVisitas>();
        servicos.AddScoped<ConsultarCasas>();
        servicos.AddScoped<CadastrarResidente>();
        servicos.AddScoped<RegistrarConsentimento>();
        servicos.AddScoped<Autenticar>();
        servicos.AddScoped<ConsultarLimiares>();
        servicos.AddScoped<ImportarAchados>();
        servicos.AddScoped<ConsultarRiscoCidadao>();
        servicos.AddScoped<AnalisarTexto>();
        return servicos;
    }
}
