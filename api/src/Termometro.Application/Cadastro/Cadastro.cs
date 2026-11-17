using Termometro.Application.Abstracoes;
using Termometro.Application.Erros;
using Termometro.Application.Risco;
using Termometro.Domain.Entidades;

namespace Termometro.Application.Cadastro;

public sealed record CadastrarResidentePedido(
    string Nome,
    string Cpf,
    DateOnly DataNascimento,
    bool MoraSozinho,
    bool TemDoencaCronica,
    string? CondicoesSaude,
    string? TelefoneContato,
    string Endereco,
    string Cep,
    TipoCobertura TipoCobertura,
    int? Andar,
    Guid? MicroareaId,
    bool ConsentimentoConcedido);

public sealed record ResidenteCadastrado(Guid ResidenteId, Guid DomicilioId, string CpfMascarado);

/// <summary>
/// Cadastra a pessoa, a casa e o consentimento juntos. Sem consentimento (LGPD) não cadastra:
/// a finalidade é dizer à pessoa PARA QUE o sensor mede a casa dela.
/// </summary>
public sealed class CadastrarResidente(ICadastroRepositorio cadastro, IUnidadeDeTrabalho unidade, ServicoDiaDeRisco relogio)
{
    public const string FinalidadePadrao = "Monitorar a temperatura da casa para prevenir riscos à saúde";

    public async Task<ResidenteCadastrado> ExecutarAsync(UsuarioAutenticado usuario,
        CadastrarResidentePedido pedido, CancellationToken ct)
    {
        Permissoes.ExigirPapel(usuario, PapelUsuario.Acs, PapelUsuario.Admin);

        if (!pedido.ConsentimentoConcedido)
        {
            throw new DadosInvalidos("É preciso o consentimento da pessoa para cadastrar e monitorar a casa.");
        }
        if (!Cpf.TentarCriar(pedido.Cpf, out var cpf))
        {
            throw new DadosInvalidos("CPF inválido.");
        }

        // ACS cadastra só na própria microárea; o admin escolhe.
        var microareaId = usuario.Papel == PapelUsuario.Acs ? usuario.MicroareaId : pedido.MicroareaId;
        if (microareaId is null || !await cadastro.MicroareaExisteAsync(microareaId.Value, ct))
        {
            throw new DadosInvalidos("Microárea inválida.");
        }
        if (await cadastro.CpfJaCadastradoAsync(cpf, ct))
        {
            throw new Conflito("Já existe um residente com este CPF.");
        }

        try
        {
            var residente = new Residente(pedido.Nome, cpf, pedido.DataNascimento, pedido.MoraSozinho,
                pedido.TemDoencaCronica, pedido.CondicoesSaude, pedido.TelefoneContato);
            var casa = new Domicilio(residente.Id, microareaId.Value, pedido.Endereco, pedido.Cep,
                pedido.TipoCobertura, pedido.Andar);
            var consentimento = new Consentimento(residente.Id, FinalidadePadrao, relogio.Agora);

            await cadastro.AdicionarResidenteAsync(residente, ct);
            await cadastro.AdicionarDomicilioAsync(casa, ct);
            await cadastro.AdicionarConsentimentoAsync(consentimento, ct);
            await unidade.SalvarAsync(ct);

            return new ResidenteCadastrado(residente.Id, casa.Id, cpf.Mascarado);
        }
        catch (ArgumentException erro)
        {
            throw new DadosInvalidos(erro.Message);
        }
    }
}

public sealed record RegistrarConsentimentoPedido(Guid ResidenteId, string Finalidade);

public sealed record ConsentimentoRegistrado(Guid Id, Guid ResidenteId, string Finalidade, DateTimeOffset ConcedidoEm);

/// <summary>Registra um novo consentimento (ex.: outra finalidade) para um residente já cadastrado.</summary>
public sealed class RegistrarConsentimento(
    ICadastroRepositorio cadastro,
    IMonitoramentoRepositorio monitoramento,
    IUnidadeDeTrabalho unidade,
    ServicoDiaDeRisco relogio)
{
    public async Task<ConsentimentoRegistrado> ExecutarAsync(UsuarioAutenticado usuario,
        RegistrarConsentimentoPedido pedido, CancellationToken ct)
    {
        Permissoes.ExigirPapel(usuario, PapelUsuario.Acs, PapelUsuario.Admin);

        if (!await cadastro.ResidenteExisteAsync(pedido.ResidenteId, ct))
        {
            throw new NaoEncontrado("Residente não encontrado.");
        }
        if (usuario.Papel == PapelUsuario.Acs)
        {
            var casa = await monitoramento.ObterDomicilioDoResidenteAsync(pedido.ResidenteId, ct);
            if (casa is null || casa.MicroareaId != usuario.MicroareaId)
            {
                throw new AcessoNegado("Este residente não é da sua microárea.");
            }
        }

        try
        {
            var consentimento = new Consentimento(pedido.ResidenteId, pedido.Finalidade, relogio.Agora);
            await cadastro.AdicionarConsentimentoAsync(consentimento, ct);
            await unidade.SalvarAsync(ct);
            return new ConsentimentoRegistrado(consentimento.Id, consentimento.ResidenteId,
                consentimento.Finalidade, consentimento.ConcedidoEm);
        }
        catch (ArgumentException erro)
        {
            throw new DadosInvalidos(erro.Message);
        }
    }
}
