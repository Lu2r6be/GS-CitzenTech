import { describe, expect, it, vi } from 'vitest';

import { ClienteApi, ErroDaApi, ErroDeRede, mensagemDeErro, SessaoExpirada } from './cliente';

interface Chamada {
  url: string;
  init: RequestInit;
}

function criar(
  resposta: { status: number; corpo?: unknown } | (() => never),
  opcoes: { token?: string | null; aoSessaoExpirar?: () => void; tempoLimiteMs?: number } = {},
) {
  const chamadas: Chamada[] = [];
  const fetchFalso = (async (url: string, init: RequestInit) => {
    chamadas.push({ url, init });
    if (typeof resposta === 'function') return resposta();
    return new Response(resposta.corpo === undefined ? '' : JSON.stringify(resposta.corpo), { status: resposta.status });
  }) as unknown as typeof fetch;
  const cliente = new ClienteApi({
    baseUrl: () => 'https://exemplo.trycloudflare.com/',
    token: () => opcoes.token ?? 'token-123',
    aoSessaoExpirar: opcoes.aoSessaoExpirar,
    fetchImpl: fetchFalso,
    tempoLimiteMs: opcoes.tempoLimiteMs,
  });
  return { cliente, chamadas };
}

describe('ClienteApi', () => {
  it('monta a URL sem barra duplicada e manda o token no cabeçalho', async () => {
    const { cliente, chamadas } = criar({ status: 200, corpo: { diaDeRisco: 'nenhum', casas: [] } });
    const fila = await cliente.fila();
    expect(fila.casas).toEqual([]);
    expect(chamadas[0].url).toBe('https://exemplo.trycloudflare.com/api/v1/visitas/fila');
    expect((chamadas[0].init.headers as Record<string, string>).Authorization).toBe('Bearer token-123');
  });

  it('o login NÃO manda token e envia e-mail e senha no corpo', async () => {
    const { cliente, chamadas } = criar({ status: 200, corpo: { token: 't', expiraEm: 'x', papel: 'Acs' } });
    await cliente.login('ana.acs@demo.local', 'segredo');
    const cabecalhos = chamadas[0].init.headers as Record<string, string>;
    expect(cabecalhos.Authorization).toBeUndefined();
    expect(JSON.parse(chamadas[0].init.body as string)).toEqual({ email: 'ana.acs@demo.local', senha: 'segredo' });
  });

  it('a sincronização manda { visitas: [...] }', async () => {
    const { cliente, chamadas } = criar({ status: 200, corpo: { visitas: [] } });
    await cliente.sincronizarVisitas([{ id: 'a', domicilioId: 'b', realizadaEm: 'c', resultado: 'tudoBem' }]);
    expect(chamadas[0].url).toContain('/api/v1/visitas/sincronizar');
    expect(JSON.parse(chamadas[0].init.body as string).visitas).toHaveLength(1);
  });

  it('falha de conexão vira ErroDeRede', async () => {
    const { cliente } = criar(() => {
      throw new TypeError('Network request failed');
    });
    await expect(cliente.fila()).rejects.toBeInstanceOf(ErroDeRede);
  });

  it('502/503/504 (túnel no ar, API fora) também vira ErroDeRede', async () => {
    for (const status of [502, 503, 504]) {
      const { cliente } = criar({ status });
      await expect(cliente.fila()).rejects.toBeInstanceOf(ErroDeRede);
    }
  });

  it('demora além do limite vira ErroDeRede', async () => {
    const lento = (async (_url: string, init: RequestInit) =>
      new Promise((_resolve, rejeitar) => {
        init.signal?.addEventListener('abort', () => rejeitar(new DOMException('abortado', 'AbortError')));
      })) as unknown as typeof fetch;
    const cliente = new ClienteApi({ baseUrl: () => 'http://x', token: () => null, fetchImpl: lento, tempoLimiteMs: 20 });
    await expect(cliente.fila()).rejects.toBeInstanceOf(ErroDeRede);
  });

  it('401 numa rota protegida avisa o app (volta ao login) e lança SessaoExpirada', async () => {
    const aoSessaoExpirar = vi.fn();
    const { cliente } = criar({ status: 401 }, { aoSessaoExpirar });
    await expect(cliente.fila()).rejects.toBeInstanceOf(SessaoExpirada);
    expect(aoSessaoExpirar).toHaveBeenCalledOnce();
  });

  it('401 no LOGIN é senha errada: mostra a mensagem da API e não derruba a sessão', async () => {
    const aoSessaoExpirar = vi.fn();
    const { cliente } = criar(
      { status: 401, corpo: { title: 'Não autenticado', detail: 'E-mail ou senha incorretos.' } },
      { aoSessaoExpirar },
    );
    const erro = await cliente.login('a@b.c', 'x').catch((e) => e);
    expect(erro).toBeInstanceOf(ErroDaApi);
    expect(erro).not.toBeInstanceOf(SessaoExpirada);
    expect(erro.message).toBe('E-mail ou senha incorretos.');
    expect(aoSessaoExpirar).not.toHaveBeenCalled();
  });

  it('400 usa o "detail" da API como mensagem', async () => {
    const { cliente } = criar({ status: 400, corpo: { title: 'Dados inválidos', detail: 'CPF inválido.' } });
    const erro = await cliente
      .cadastrarResidente({} as never)
      .catch((e) => e);
    expect(erro.status).toBe(400);
    expect(erro.message).toBe('CPF inválido.');
  });
});

describe('mensagemDeErro', () => {
  it('429 pede para esperar', () => {
    expect(mensagemDeErro(429, null)).toMatch(/Espere um minuto/);
  });

  it('500 nunca repete o que o servidor mandou', () => {
    expect(mensagemDeErro(500, { detail: 'Npgsql: senha do banco...' })).not.toMatch(/Npgsql/);
  });

  it('erro de validação do ASP.NET: usa a primeira mensagem do campo', () => {
    expect(mensagemDeErro(400, { title: 'One or more validation errors occurred.', errors: { pedido: ['The pedido field is required.'] } })).toBe(
      'The pedido field is required.',
    );
  });

  it('sem corpo útil: frase genérica em português', () => {
    expect(mensagemDeErro(418, null)).toBe('Algo deu errado. Tente de novo.');
  });
});
