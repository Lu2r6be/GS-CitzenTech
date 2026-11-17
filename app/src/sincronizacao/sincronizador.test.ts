import { beforeEach, describe, expect, it } from 'vitest';

import { ErroDaApi, ErroDeRede } from '@/api/cliente';
import type { ResultadoDaSincronizacao, VisitaParaEnviar } from '@/api/tipos';
import { prepararBanco } from '@/banco/esquema';
import { VisitasLocais } from '@/banco/repositorios';
import { criarBancoEmMemoria } from '@/testes/bancoEmMemoria';

import { criarSincronizador, TAMANHO_DO_LOTE } from './sincronizador';

let visitas: VisitasLocais;

async function criarVisitas(quantas: number, prefixo = 'v') {
  for (let i = 0; i < quantas; i += 1) {
    await visitas.registrar({
      id: `${prefixo}${i}`,
      domicilioId: 'casa-1',
      realizadaEm: new Date(Date.UTC(2026, 10, 16, 8, 0, i)).toISOString(),
      resultado: 'orientado',
      observacao: null,
    });
  }
}

/** API de mentira: aceita tudo, ou responde como o teste mandar. */
function apiDeMentira(responder: (lote: VisitaParaEnviar[]) => Promise<ResultadoDaSincronizacao>) {
  const chamadas: VisitaParaEnviar[][] = [];
  return {
    chamadas,
    sincronizarVisitas: (lote: VisitaParaEnviar[]) => {
      chamadas.push(lote);
      return responder(lote);
    },
  };
}

const aceitaTudo = async (lote: VisitaParaEnviar[]): Promise<ResultadoDaSincronizacao> => ({
  visitas: lote.map((v) => ({ id: v.id, status: 'criada' as const })),
});

beforeEach(async () => {
  const banco = criarBancoEmMemoria();
  await prepararBanco(banco);
  visitas = new VisitasLocais(banco);
});

describe('sincronizador', () => {
  it('envia as pendentes e marca como sincronizadas', async () => {
    await criarVisitas(3);
    const api = apiDeMentira(aceitaTudo);
    const resumo = await criarSincronizador(api, visitas)();

    expect(resumo).toEqual({ enviadas: 3, jaExistiam: 0, rejeitadas: 0, restantes: 0, semRede: false });
    expect(api.chamadas).toHaveLength(1);
    expect(await visitas.contarPendentes()).toBe(0);
  });

  it('sem visita pendente nem chama a API', async () => {
    const api = apiDeMentira(aceitaTudo);
    const resumo = await criarSincronizador(api, visitas)();
    expect(api.chamadas).toHaveLength(0);
    expect(resumo.restantes).toBe(0);
  });

  it('sem rede: nada se perde, tudo continua pendente e o resumo avisa', async () => {
    await criarVisitas(2);
    const api = apiDeMentira(async () => {
      throw new ErroDeRede();
    });
    const resumo = await criarSincronizador(api, visitas)();
    expect(resumo).toMatchObject({ semRede: true, restantes: 2, enviadas: 0 });
    expect(await visitas.contarPendentes()).toBe(2);
  });

  it('depois que a rede volta, a mesma visita é reenviada com o MESMO id', async () => {
    await criarVisitas(1);
    let falhar = true;
    const api = apiDeMentira(async (lote) => {
      if (falhar) throw new ErroDeRede();
      return aceitaTudo(lote);
    });
    const sincronizar = criarSincronizador(api, visitas);

    await sincronizar();
    falhar = false;
    const resumo = await sincronizar();

    expect(resumo.enviadas).toBe(1);
    expect(api.chamadas.map((lote) => lote[0].id)).toEqual(['v0', 'v0']);
  });

  it('"já existia" conta como entregue (o servidor já tinha recebido antes)', async () => {
    await criarVisitas(1);
    const api = apiDeMentira(async (lote) => ({ visitas: lote.map((v) => ({ id: v.id, status: 'jaExistia' as const })) }));
    const resumo = await criarSincronizador(api, visitas)();
    expect(resumo).toMatchObject({ jaExistiam: 1, enviadas: 0, restantes: 0 });
  });

  it('um item recusado não derruba os outros e guarda o motivo', async () => {
    await criarVisitas(3);
    const api = apiDeMentira(async (lote) => ({
      visitas: lote.map((v) =>
        v.id === 'v1'
          ? { id: v.id, status: 'rejeitada' as const, motivo: 'A data da visita está no futuro' }
          : { id: v.id, status: 'criada' as const },
      ),
    }));
    const resumo = await criarSincronizador(api, visitas)();

    expect(resumo).toMatchObject({ enviadas: 2, rejeitadas: 1, restantes: 0 });
    const [recusada] = await visitas.rejeitadas();
    expect(recusada).toMatchObject({ id: 'v1', motivoRejeicao: 'A data da visita está no futuro' });
  });

  it('se o servidor não confirma um id, ele continua pendente (nunca "sincroniza" sem confirmação)', async () => {
    await criarVisitas(2);
    const api = apiDeMentira(async () => ({ visitas: [{ id: 'v0', status: 'criada' as const }] }));
    const resumo = await criarSincronizador(api, visitas)();
    expect(resumo).toMatchObject({ enviadas: 1, restantes: 1 });
    expect((await visitas.pendentes()).map((v) => v.id)).toEqual(['v1']);
  });

  it('divide em lotes de no máximo 200 (limite da API)', async () => {
    await criarVisitas(TAMANHO_DO_LOTE + 5);
    const api = apiDeMentira(aceitaTudo);
    const resumo = await criarSincronizador(api, visitas)();
    expect(api.chamadas.map((l) => l.length)).toEqual([TAMANHO_DO_LOTE, 5]);
    expect(resumo.enviadas).toBe(TAMANHO_DO_LOTE + 5);
  });

  it('duas chamadas ao mesmo tempo viram uma só (foco da tela + botão)', async () => {
    await criarVisitas(2);
    const api = apiDeMentira(aceitaTudo);
    const sincronizar = criarSincronizador(api, visitas);
    const [a, b] = await Promise.all([sincronizar(), sincronizar()]);
    expect(api.chamadas).toHaveLength(1);
    expect(a).toBe(b);
  });

  it('erro do servidor (não é falta de rede) sobe para quem chamou e as visitas ficam guardadas', async () => {
    await criarVisitas(1);
    const api = apiDeMentira(async () => {
      throw new ErroDaApi(500, 'O servidor teve um problema.');
    });
    await expect(criarSincronizador(api, visitas)()).rejects.toThrow('O servidor teve um problema.');
    expect(await visitas.contarPendentes()).toBe(1);
  });

  it('depois de terminar, uma nova chamada roda de novo (não fica "travada")', async () => {
    await criarVisitas(1);
    const api = apiDeMentira(aceitaTudo);
    const sincronizar = criarSincronizador(api, visitas);
    await sincronizar();
    await criarVisitas(1, 'novo');
    const resumo = await sincronizar();
    expect(resumo.enviadas).toBe(1);
    expect(api.chamadas).toHaveLength(2);
  });
});
