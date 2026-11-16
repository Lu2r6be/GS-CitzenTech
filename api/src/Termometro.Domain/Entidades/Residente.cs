using Termometro.Domain.Fila;
using Termometro.Domain.Seguranca;

namespace Termometro.Domain.Entidades;

/// <summary>A pessoa idosa acompanhada. Quase tudo aqui é PII (LGPD): cuidado com logs.</summary>
public sealed class Residente
{
    public Guid Id { get; private set; }

    [Pii(CategoriaPii.Nome)]
    public string Nome { get; private set; } = string.Empty;

    [Pii(CategoriaPii.Cpf)]
    public Cpf Cpf { get; private set; } = null!;

    public DateOnly DataNascimento { get; private set; }

    [Pii(CategoriaPii.Saude)]
    public string? CondicoesSaude { get; private set; }

    [Pii(CategoriaPii.Contato)]
    public string? TelefoneContato { get; private set; }

    // Usados só para a ORDEM da fila; nunca mudam a cor do semáforo.
    [Pii(CategoriaPii.Saude)]
    public bool MoraSozinho { get; private set; }

    [Pii(CategoriaPii.Saude)]
    public bool TemDoencaCronica { get; private set; }

    private Residente() { } // usado pelo EF Core

    public Residente(string nome, Cpf cpf, DateOnly dataNascimento, bool moraSozinho,
        bool temDoencaCronica, string? condicoesSaude = null, string? telefoneContato = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentNullException.ThrowIfNull(cpf);
        if (dataNascimento == default)
        {
            throw new ArgumentException("Data de nascimento obrigatória.", nameof(dataNascimento));
        }

        Id = Guid.CreateVersion7();
        Nome = nome.Trim();
        Cpf = cpf;
        DataNascimento = dataNascimento;
        MoraSozinho = moraSozinho;
        TemDoencaCronica = temDoencaCronica;
        CondicoesSaude = condicoesSaude;
        TelefoneContato = telefoneContato;
    }

    public int IdadeEm(DateOnly hoje)
    {
        var idade = hoje.Year - DataNascimento.Year;
        return DataNascimento.AddYears(idade) > hoje ? idade - 1 : idade;
    }

    public Vulnerabilidade VulnerabilidadeEm(DateOnly hoje) =>
        new(IdadeEm(hoje), MoraSozinho, TemDoencaCronica);
}
