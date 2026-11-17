using Microsoft.EntityFrameworkCore;
using Termometro.Application.Abstracoes;
using Termometro.Domain.Entidades;
using Termometro.Infrastructure.Persistencia;

namespace Termometro.Infrastructure.Repositorios;

public sealed class CadastroRepositorio(TermometroDbContext banco) : ICadastroRepositorio
{
    public Task<Usuario?> ObterUsuarioPorEmailAsync(string email, CancellationToken ct) =>
        banco.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> MicroareaExisteAsync(Guid id, CancellationToken ct) =>
        banco.Microareas.AnyAsync(m => m.Id == id, ct);

    public Task<bool> ResidenteExisteAsync(Guid id, CancellationToken ct) =>
        banco.Residentes.AnyAsync(r => r.Id == id, ct);

    public Task<bool> CpfJaCadastradoAsync(Cpf cpf, CancellationToken ct) =>
        banco.Residentes.AnyAsync(r => r.Cpf == cpf, ct);

    public async Task AdicionarResidenteAsync(Residente residente, CancellationToken ct) =>
        await banco.Residentes.AddAsync(residente, ct);

    public async Task AdicionarDomicilioAsync(Domicilio domicilio, CancellationToken ct) =>
        await banco.Domicilios.AddAsync(domicilio, ct);

    public async Task AdicionarConsentimentoAsync(Consentimento consentimento, CancellationToken ct) =>
        await banco.Consentimentos.AddAsync(consentimento, ct);
}
