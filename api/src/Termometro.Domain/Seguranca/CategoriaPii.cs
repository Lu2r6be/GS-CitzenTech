namespace Termometro.Domain.Seguranca;

/// <summary>Tipos de dado pessoal (ou segredo) que o módulo de segurança precisa detectar e proteger.</summary>
public enum CategoriaPii
{
    Nome = 0,
    Cpf = 1,
    Saude = 2,
    Endereco = 3,
    Contato = 4,
    Email = 5,
    Senha = 6,
    /// <summary>Chaves, tokens e credenciais (JWT, chave de API). Não é "pessoal", mas vaza igual.</summary>
    Segredo = 7,
    /// <summary>Documento de identidade (RG). Como o CPF, identifica a pessoa e não pode sair em log nem em resposta.</summary>
    Documento = 8,
}
