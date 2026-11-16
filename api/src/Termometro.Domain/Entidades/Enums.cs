namespace Termometro.Domain.Entidades;

public enum PapelUsuario { Acs = 0, Familiar = 1, Admin = 2 }

public enum TipoCobertura { Laje = 0, Telha = 1, Fibrocimento = 2, Outro = 3 }

public enum OrigemVisita { Online = 0, Offline = 1 }

public enum ResultadoVisita
{
    NaoRealizada = 0,
    TudoBem = 1,
    Orientado = 2,
    EncaminhadoUbs = 3,
    NaoEncontrado = 4,
}

public enum SeveridadeAchado { Baixa = 0, Media = 1, Alta = 2, Critica = 3 }

/// <summary>Por onde um dado sensível quase vazou (os "sumidouros" do taint analysis).</summary>
public enum ExposicaoAchado { Log = 0, RespostaHttp = 1, PromptLlm = 2, Arquivo = 3 }
