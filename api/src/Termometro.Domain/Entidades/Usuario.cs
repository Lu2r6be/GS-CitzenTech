using Termometro.Domain.Seguranca;

namespace Termometro.Domain.Entidades;

/// <summary>Quem faz login: ACS (vê uma microárea), Familiar (vê uma casa) ou Admin.</summary>
public sealed class Usuario
{
    public Guid Id { get; private set; }

    [Pii(CategoriaPii.Nome)]
    public string Nome { get; private set; } = string.Empty;

    [Pii(CategoriaPii.Email)]
    public string Email { get; private set; } = string.Empty;

    /// <summary>Hash da senha (nunca a senha). Mesmo assim é marcado como PII por precaução.</summary>
    [Pii(CategoriaPii.Senha)]
    public string SenhaHash { get; private set; } = string.Empty;

    public PapelUsuario Papel { get; private set; }
    public Guid? MicroareaId { get; private set; }
    public Guid? ResidenteId { get; private set; }

    private Usuario() { }

    private Usuario(string nome, string email, string senhaHash, PapelUsuario papel,
        Guid? microareaId, Guid? residenteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(senhaHash);
        if (!email.Contains('@'))
        {
            throw new ArgumentException("E-mail inválido.", nameof(email));
        }

        Id = Guid.CreateVersion7();
        Nome = nome.Trim();
        Email = email.Trim().ToLowerInvariant();
        SenhaHash = senhaHash;
        Papel = papel;
        MicroareaId = microareaId;
        ResidenteId = residenteId;
    }

    public static Usuario CriarAcs(string nome, string email, string senhaHash, Guid microareaId) =>
        microareaId == Guid.Empty
            ? throw new ArgumentException("O ACS precisa de uma microárea.", nameof(microareaId))
            : new(nome, email, senhaHash, PapelUsuario.Acs, microareaId, null);

    public static Usuario CriarFamiliar(string nome, string email, string senhaHash, Guid residenteId) =>
        residenteId == Guid.Empty
            ? throw new ArgumentException("O familiar precisa de um residente.", nameof(residenteId))
            : new(nome, email, senhaHash, PapelUsuario.Familiar, null, residenteId);

    public static Usuario CriarAdmin(string nome, string email, string senhaHash) =>
        new(nome, email, senhaHash, PapelUsuario.Admin, null, null);
}
