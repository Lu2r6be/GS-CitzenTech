using Termometro.Application.Abstracoes;
using Termometro.Application.Erros;
using Termometro.Domain.Entidades;

namespace Termometro.Application;

/// <summary>
/// Quem pode ver o quê. Os controllers já filtram por papel; aqui está a regra de DADOS
/// (ACS só a própria microárea, familiar só a própria casa). Duas camadas: se uma falhar, a outra segura.
/// </summary>
internal static class Permissoes
{
    public static void ExigirPapel(UsuarioAutenticado usuario, params PapelUsuario[] permitidos)
    {
        if (!permitidos.Contains(usuario.Papel))
        {
            throw new AcessoNegado("Seu perfil não tem acesso a este recurso.");
        }
    }

    public static void ExigirAcessoACasa(UsuarioAutenticado usuario, Domicilio casa)
    {
        var permitido = usuario.Papel switch
        {
            PapelUsuario.Admin => true,
            PapelUsuario.Acs => usuario.MicroareaId == casa.MicroareaId,
            PapelUsuario.Familiar => usuario.ResidenteId == casa.ResidenteId,
            _ => false,
        };

        if (!permitido)
        {
            throw new AcessoNegado("Você não tem acesso a esta casa.");
        }
    }
}
