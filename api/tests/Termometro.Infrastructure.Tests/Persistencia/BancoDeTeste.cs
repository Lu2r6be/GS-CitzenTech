using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Termometro.Infrastructure.Persistencia;

namespace Termometro.Infrastructure.Tests.Persistencia;

/// <summary>
/// Banco SQLite em memória, criado do zero para cada teste. A conexão fica aberta enquanto o teste
/// roda (se fechasse, o banco em memória sumiria). Cada <see cref="NovoContexto"/> simula uma
/// requisição diferente da API: nada fica "de graça" no cache do EF Core.
/// </summary>
public sealed class BancoDeTeste : IDisposable
{
    private readonly SqliteConnection _conexao = new("DataSource=:memory:");
    private readonly DbContextOptions<TermometroDbContext> _opcoes;

    public BancoDeTeste(TimeProvider? relogio = null)
    {
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<TermometroDbContext>().UseSqlite(_conexao).Options;
        Relogio = relogio;

        using var contexto = NovoContexto();
        contexto.Database.EnsureCreated();
    }

    public TimeProvider? Relogio { get; }

    public TermometroDbContext NovoContexto() => new(_opcoes, Relogio);

    /// <summary>Lê uma coluna crua, sem passar pelo EF Core (para ver o que realmente foi gravado).</summary>
    public T LerColuna<T>(string sql)
    {
        using var comando = _conexao.CreateCommand();
        comando.CommandText = sql;
        return (T)Convert.ChangeType(comando.ExecuteScalar()!, typeof(T));
    }

    public void Dispose() => _conexao.Dispose();
}

public sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}
