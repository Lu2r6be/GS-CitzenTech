namespace Termometro.Application.Erros;

/// <summary>
/// Erros esperados dos casos de uso. A camada Api traduz cada um em um código HTTP,
/// então os casos de uso não precisam saber nada de HTTP.
/// </summary>
public abstract class ErroDeAplicacao(string mensagem) : Exception(mensagem);

/// <summary>400: o dado enviado não faz sentido (ex.: umidade 150%).</summary>
public sealed class DadosInvalidos(string mensagem) : ErroDeAplicacao(mensagem);

/// <summary>401: não sabemos quem é (login errado, chave do dispositivo errada).</summary>
public sealed class NaoAutenticado(string mensagem) : ErroDeAplicacao(mensagem);

/// <summary>403: sabemos quem é, mas não pode (ACS tentando ver casa de outra microárea).</summary>
public sealed class AcessoNegado(string mensagem) : ErroDeAplicacao(mensagem);

/// <summary>404.</summary>
public sealed class NaoEncontrado(string mensagem) : ErroDeAplicacao(mensagem);

/// <summary>409: conflito com o que já existe (ex.: CPF já cadastrado).</summary>
public sealed class Conflito(string mensagem) : ErroDeAplicacao(mensagem);
