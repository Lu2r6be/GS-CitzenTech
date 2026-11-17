using System.Security.Cryptography;
using System.Text;
using Termometro.Application.Abstracoes;

namespace Termometro.Infrastructure.Seguranca;

/// <summary>
/// PBKDF2-SHA256 com sal aleatório. Formato guardado: "pbkdf2$iterações$sal$hash" (base64).
/// Guardar as iterações junto permite aumentá-las no futuro sem invalidar senhas antigas.
/// </summary>
public sealed class HashDeSenhaPbkdf2 : IHashDeSenha
{
    private const int Iteracoes = 210_000; // recomendação OWASP para PBKDF2-HMAC-SHA256
    private const int TamanhoDoSal = 16;
    private const int TamanhoDoHash = 32;

    public string Gerar(string senha)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanhoDoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoDoHash);
        return $"pbkdf2${Iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string senha, string hashGuardado)
    {
        var partes = hashGuardado.Split('$');
        if (partes is not ["pbkdf2", var iteracoesTexto, var salTexto, var hashTexto]
            || !int.TryParse(iteracoesTexto, out var iteracoes) || iteracoes <= 0)
        {
            return false;
        }

        try
        {
            var sal = Convert.FromBase64String(salTexto);
            var esperado = Convert.FromBase64String(hashTexto);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(senha, sal, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>SHA-256 simples: a chave do dispositivo é longa e aleatória, não uma senha que gente escolhe.</summary>
public sealed class HashDeChaveDeDispositivoSha256 : IHashDeChaveDeDispositivo
{
    public string Gerar(string chave) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chave)));

    public bool Verificar(string chave, string hash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Gerar(chave)), Encoding.ASCII.GetBytes(hash.ToUpperInvariant()));
}
