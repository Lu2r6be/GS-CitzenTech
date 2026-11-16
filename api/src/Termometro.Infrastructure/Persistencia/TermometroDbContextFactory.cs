using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Termometro.Infrastructure.Persistencia;

/// <summary>
/// Usado só pela ferramenta `dotnet ef` para gerar migrations. Gerar migration NÃO conecta no banco,
/// por isso a senha abaixo é falsa. A conexão real vem da configuração da API.
/// </summary>
public sealed class TermometroDbContextFactory : IDesignTimeDbContextFactory<TermometroDbContext>
{
    public TermometroDbContext CreateDbContext(string[] args)
    {
        var opcoes = new DbContextOptionsBuilder<TermometroDbContext>()
            .UseNpgsql("Host=localhost;Database=termometro;Username=termometro;Password=somente-design-time")
            .Options;
        return new TermometroDbContext(opcoes);
    }
}
