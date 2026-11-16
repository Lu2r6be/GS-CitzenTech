using Microsoft.EntityFrameworkCore;
using Termometro.Infrastructure.Persistencia;

namespace Termometro.Infrastructure.Tests.Persistencia;

/// <summary>
/// Conferências do PostgreSQL que NÃO precisam de um servidor rodando:
/// só olham o modelo e as migrations.
/// </summary>
public class MigrationsPostgresTests
{
    private static TermometroDbContext NovoContextoPostgres()
    {
        var opcoes = new DbContextOptionsBuilder<TermometroDbContext>()
            .UseNpgsql("Host=localhost;Database=teste;Username=teste;Password=teste")
            .Options;
        return new TermometroDbContext(opcoes);
    }

    [Fact]
    public void Migrations_EstaoEmDiaComOModeloDoCodigo()
    {
        using var ctx = NovoContextoPostgres();

        // Se alguém mudar uma entidade e esquecer de gerar a migration, este teste falha.
        Assert.False(ctx.Database.HasPendingModelChanges(),
            "O modelo mudou: gere uma nova migration (dotnet ef migrations add ...).");
    }

    [Fact]
    public void Migrations_ExisteAoMenosUmaMigration()
    {
        using var ctx = NovoContextoPostgres();

        Assert.NotEmpty(ctx.Database.GetMigrations());
    }

    [Fact]
    public void ScriptDoPostgres_CriaAsTabelasComOsTiposCertos()
    {
        using var ctx = NovoContextoPostgres();

        var sql = ctx.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE \"Leituras\"", sql);
        Assert.Contains("CREATE TABLE \"Domicilios\"", sql);
        Assert.Contains("timestamp with time zone", sql);
        Assert.Contains("uuid", sql);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Residentes_Cpf\"", sql);
    }

    [Fact]
    public void ScriptDoPostgres_NaoTemColunaParaOCpfMascarado()
    {
        using var ctx = NovoContextoPostgres();

        var sql = ctx.Database.GenerateCreateScript();

        Assert.DoesNotContain("Mascarado", sql);
    }
}
