import type {
  CadastroDeResidente,
  CasaDoFamiliar,
  DetalheDaCasa,
  FilaDeVisitas,
  LoginResposta,
  ResidenteCadastrado,
  ResultadoDaSincronizacao,
  VisitaParaEnviar,
} from './tipos';

/** Não deu para falar com o servidor (sem internet, túnel fora do ar, demorou demais). */
export class ErroDeRede extends Error {
  constructor() {
    super('Sem conexão com o servidor.');
    this.name = 'ErroDeRede';
  }
}

/** O servidor respondeu, mas com erro (400, 403, 404, 500...). A mensagem já vem em português simples. */
export class ErroDaApi extends Error {
  constructor(
    public readonly status: number,
    mensagem: string,
  ) {
    super(mensagem);
    this.name = 'ErroDaApi';
  }
}

/** O token venceu ou não vale mais: o app precisa levar a pessoa de volta ao login. */
export class SessaoExpirada extends ErroDaApi {
  constructor() {
    super(401, 'Sua sessão terminou. Entre de novo.');
    this.name = 'SessaoExpirada';
  }
}

export interface OpcoesDoCliente {
  /** Função (e não texto fixo) porque a pessoa pode trocar o endereço do servidor com o app aberto. */
  baseUrl: () => string;
  token: () => string | null;
  aoSessaoExpirar?: () => void;
  fetchImpl?: typeof fetch;
  tempoLimiteMs?: number;
}

interface Problema {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Transforma a resposta de erro da API (ProblemDetails) em uma frase que a pessoa entende. */
export function mensagemDeErro(status: number, corpo: unknown): string {
  if (status === 429) return 'Muitas tentativas. Espere um minuto e tente de novo.';
  if (status >= 500) return 'O servidor teve um problema. Tente de novo em instantes.';
  if (corpo && typeof corpo === 'object') {
    const problema = corpo as Problema;
    if (problema.detail) return problema.detail;
    const primeira = problema.errors ? Object.values(problema.errors).flat()[0] : undefined;
    if (primeira) return primeira;
    if (problema.title) return problema.title;
  }
  return 'Algo deu errado. Tente de novo.';
}

/**
 * Único ponto do app que fala com a API. As telas chamam métodos com nome de negócio
 * (fila, casa, sincronizar...) e nunca montam URL ou cabeçalho.
 */
export class ClienteApi {
  private readonly fetchImpl: typeof fetch;
  private readonly tempoLimiteMs: number;

  constructor(private readonly opcoes: OpcoesDoCliente) {
    this.fetchImpl = opcoes.fetchImpl ?? ((...args) => fetch(...args));
    this.tempoLimiteMs = opcoes.tempoLimiteMs ?? 10_000;
  }

  login(email: string, senha: string): Promise<LoginResposta> {
    return this.requisitar('POST', '/api/v1/auth/login', { email, senha }, false);
  }

  fila(): Promise<FilaDeVisitas> {
    return this.requisitar('GET', '/api/v1/visitas/fila');
  }

  casa(domicilioId: string): Promise<DetalheDaCasa> {
    return this.requisitar('GET', `/api/v1/domicilios/${domicilioId}`);
  }

  casaDoFamiliar(): Promise<CasaDoFamiliar> {
    return this.requisitar('GET', '/api/v1/familiar/casa');
  }

  sincronizarVisitas(visitas: VisitaParaEnviar[]): Promise<ResultadoDaSincronizacao> {
    return this.requisitar('POST', '/api/v1/visitas/sincronizar', { visitas });
  }

  cadastrarResidente(pedido: CadastroDeResidente): Promise<ResidenteCadastrado> {
    return this.requisitar('POST', '/api/v1/residentes', pedido);
  }

  private async requisitar<T>(
    metodo: 'GET' | 'POST',
    caminho: string,
    corpo?: unknown,
    autenticado = true,
  ): Promise<T> {
    const url = this.opcoes.baseUrl().replace(/\/+$/, '') + caminho;
    const cabecalhos: Record<string, string> = { Accept: 'application/json' };
    if (corpo !== undefined) cabecalhos['Content-Type'] = 'application/json';
    const token = this.opcoes.token();
    if (autenticado && token) cabecalhos.Authorization = `Bearer ${token}`;

    const controle = new AbortController();
    const cronometro = setTimeout(() => controle.abort(), this.tempoLimiteMs);
    let status: number;
    let texto: string;
    try {
      const resposta = await this.fetchImpl(url, {
        method: metodo,
        headers: cabecalhos,
        body: corpo === undefined ? undefined : JSON.stringify(corpo),
        signal: controle.signal,
      });
      status = resposta.status;
      texto = await resposta.text();
    } catch {
      throw new ErroDeRede();
    } finally {
      clearTimeout(cronometro);
    }

    let json: unknown = null;
    if (texto) {
      try {
        json = JSON.parse(texto);
      } catch {
        json = null;
      }
    }

    if (status >= 200 && status < 300) return json as T;

    // Túnel no ar mas API fora (502/503/504): para o app é o mesmo que estar sem internet.
    if (status === 502 || status === 503 || status === 504) throw new ErroDeRede();

    if (status === 401 && autenticado) {
      this.opcoes.aoSessaoExpirar?.();
      throw new SessaoExpirada();
    }
    throw new ErroDaApi(status, mensagemDeErro(status, json));
  }
}
