using Termometro.Domain.Entidades;

namespace Termometro.Application.Abstracoes;

/// <summary>Hash lento (PBKDF2) para SENHAS de pessoas: lento de propósito, para dificultar adivinhar.</summary>
public interface IHashDeSenha
{
    string Gerar(string senha);
    bool Verificar(string senha, string hash);
}

/// <summary>Hash rápido (SHA-256) para a CHAVE do dispositivo, que já é um segredo longo e aleatório.</summary>
public interface IHashDeChaveDeDispositivo
{
    string Gerar(string chave);
    bool Verificar(string chave, string hash);
}

public interface IEmissorDeToken
{
    TokenEmitido Emitir(Usuario usuario);
}

public sealed record TokenEmitido(string Token, DateTimeOffset ExpiraEm);

/// <summary>Quem está fazendo a requisição (vem do JWT). Os casos de uso decidem o que cada papel pode.</summary>
public sealed record UsuarioAutenticado(Guid Id, PapelUsuario Papel, Guid? MicroareaId, Guid? ResidenteId);
