using Microsoft.EntityFrameworkCore;
using Termometro.Domain.Entidades;
using Termometro.Domain.Niveis;

namespace Termometro.Infrastructure.Persistencia;

/// <summary>
/// Ponte entre as entidades do Domain e o banco (PostgreSQL em produção, SQLite nos testes).
/// As relações são só por Id (sem propriedades de navegação): cada entidade é independente,
/// e as consultas ligam as tabelas com LINQ (join).
/// </summary>
public class TermometroDbContext : DbContext
{
    private readonly TimeProvider _relogio;

    public TermometroDbContext(DbContextOptions<TermometroDbContext> options, TimeProvider? relogio = null)
        : base(options)
    {
        _relogio = relogio ?? TimeProvider.System;
    }

    public DbSet<Residente> Residentes => Set<Residente>();
    public DbSet<Microarea> Microareas => Set<Microarea>();
    public DbSet<Domicilio> Domicilios => Set<Domicilio>();
    public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();
    public DbSet<Leitura> Leituras => Set<Leitura>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Consentimento> Consentimentos => Set<Consentimento>();
    public DbSet<Visita> Visitas => Set<Visita>();
    public DbSet<ClimaExterno> ClimasExternos => Set<ClimaExterno>();
    public DbSet<DiaRisco> DiasRisco => Set<DiaRisco>();
    public DbSet<LimiarTemperatura> Limiares => Set<LimiarTemperatura>();
    public DbSet<AchadoSeguranca> Achados => Set<AchadoSeguranca>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configuracao)
    {
        configuracao.Properties<DateTimeOffset>().HaveConversion<ConversorDataHoraUtc>();
    }

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.ApplyConfigurationsFromAssembly(typeof(TermometroDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        CarimbarCriacaoDosLimiares();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        CarimbarCriacaoDosLimiares();
        return base.SaveChanges();
    }

    // O Domain não lê o relógio; quem carimba a data de criação é a camada de dados.
    private void CarimbarCriacaoDosLimiares()
    {
        var agora = _relogio.GetUtcNow();
        foreach (var entrada in ChangeTracker.Entries<LimiarTemperatura>()
                     .Where(e => e.State == EntityState.Added))
        {
            entrada.Property(l => l.CriadoEm).CurrentValue = agora;
        }
    }
}
