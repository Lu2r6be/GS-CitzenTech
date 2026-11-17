using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Termometro.Application.Abstracoes;
using Termometro.Domain.Entidades;

namespace Termometro.Api.Autenticacao;

public sealed class OpcoesJwt
{
    public const string Secao = "Jwt";
    public const int TamanhoMinimoDaChave = 32;

    public string Chave { get; set; } = string.Empty;
    public string Emissor { get; set; } = "termometro-api";
    public string Audiencia { get; set; } = "termometro-app";
    public int MinutosDeValidade { get; set; } = 480; // um turno de trabalho do ACS
}

/// <summary>Nomes dos campos (claims) do token. Curtos e fixos, para app e API combinarem.</summary>
public static class Claims
{
    public const string Id = "sub";
    public const string Papel = "role";
    public const string Microarea = "microarea";
    public const string Residente = "residente";
}

public sealed class EmissorDeTokenJwt(Microsoft.Extensions.Options.IOptions<OpcoesJwt> opcoes, TimeProvider relogio)
    : IEmissorDeToken
{
    private readonly OpcoesJwt _opcoes = opcoes.Value;

    public TokenEmitido Emitir(Usuario usuario)
    {
        var agora = relogio.GetUtcNow();
        var expiraEm = agora.AddMinutes(_opcoes.MinutosDeValidade);

        var claims = new Dictionary<string, object>
        {
            [Claims.Id] = usuario.Id.ToString(),
            [Claims.Papel] = usuario.Papel.ToString(),
        };
        if (usuario.MicroareaId is { } microarea) claims[Claims.Microarea] = microarea.ToString();
        if (usuario.ResidenteId is { } residente) claims[Claims.Residente] = residente.ToString();

        var descritor = new SecurityTokenDescriptor
        {
            Issuer = _opcoes.Emissor,
            Audience = _opcoes.Audiencia,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            Claims = claims,
            // O token NÃO leva nome, e-mail nem CPF: quem vazar o token não vaza PII junto.
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.Chave)), SecurityAlgorithms.HmacSha256),
        };

        return new TokenEmitido(new JsonWebTokenHandler().CreateToken(descritor), expiraEm);
    }
}

public static class ClaimsExtensoes
{
    /// <summary>Converte o token já validado no "quem é você" que os casos de uso entendem.</summary>
    public static UsuarioAutenticado ParaUsuarioAutenticado(this ClaimsPrincipal principal)
    {
        Guid? Guia(string tipo) => Guid.TryParse(principal.FindFirstValue(tipo), out var g) ? g : null;

        var id = Guia(Claims.Id) ?? throw new InvalidOperationException("Token sem identificador.");
        var papel = Enum.TryParse<PapelUsuario>(principal.FindFirstValue(Claims.Papel), out var p)
            ? p
            : throw new InvalidOperationException("Token sem papel válido.");

        return new UsuarioAutenticado(id, papel, Guia(Claims.Microarea), Guia(Claims.Residente));
    }
}
