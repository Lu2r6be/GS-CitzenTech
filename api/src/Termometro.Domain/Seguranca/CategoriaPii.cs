namespace Termometro.Domain.Seguranca;

/// <summary>Tipos de dado pessoal que o módulo de segurança precisa detectar e proteger.</summary>
public enum CategoriaPii
{
    Nome = 0,
    Cpf = 1,
    Saude = 2,
    Endereco = 3,
    Contato = 4,
    Email = 5,
    Senha = 6,
}
