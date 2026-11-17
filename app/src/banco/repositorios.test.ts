import { beforeEach, describe, expect, it } from 'vitest';

import { criarBancoEmMemoria } from '@/testes/bancoEmMemoria';

import type { BancoLocal } from './BancoLocal';
import { prepararBanco } from './esquema';
import { CacheLocal, Configuracao, VisitasLocais } from './repositorios';

let banco: BancoLocal;
let visitas: VisitasLocais;

const visita = (id: string, extra: Partial<Parameters<VisitasLocais['registrar']>[0]> = {}) => ({
  id,
  domicilioId: 'casa-1',
  realizadaEm: '2026-11-16T10:00:00.000Z',
  resultado: 'tudoBem' as const,
  observacao: null,
  ...extra,
});

beforeEach(async () => {
  banco = criarBancoEmMemoria();
  await prepararBanco(banco);
  visitas = new VisitasLocais(banco);
});

describe('esquema', () => {
  it('pode rodar duas vezes sem erro (o app roda toda vez que abre)', async () => {
    await expect(prepararBanco(banco)).resolves.toBeUndefined();
  });
});

describe('VisitasLocais', () => {
  it('visita registrada nasce pendente', async () => {
    await visitas.registrar(visita('v1', { observacao: 'Bebendo água' }));
    const [v] = await visitas.pendentes();
    expect(v).toMatchObject({ id: 'v1', estado: 'pendente', observacao: 'Bebendo água', resultado: 'tudoBem' });
  });

  it('o mesmo id não entra duas vezes (chave primária protege contra duplicar)', async () => {
    await visitas.registrar(visita('v1'));
    await expect(visitas.registrar(visita('v1'))).rejects.toThrow();
    expect(await visitas.contarPendentes()).toBe(1);
  });

  it('pendentes saem na ordem em que as visitas foram feitas', async () => {
    await visitas.registrar(visita('tarde', { realizadaEm: '2026-11-16T15:00:00.000Z' }));
    await visitas.registrar(visita('cedo', { realizadaEm: '2026-11-16T08:00:00.000Z' }));
    expect((await visitas.pendentes()).map((v) => v.id)).toEqual(['cedo', 'tarde']);
  });

  it('marcar como sincronizada tira da lista de pendentes e guarda o momento', async () => {
    await visitas.registrar(visita('v1'));
    await visitas.marcarSincronizada('v1', new Date('2026-11-16T12:00:00Z'));
    expect(await visitas.contarPendentes()).toBe(0);
    const [linha] = await banco.consultar<{ estado: string; sincronizada_em: string }>(
      'SELECT estado, sincronizada_em FROM visita_local WHERE id = ?',
      ['v1'],
    );
    expect(linha).toEqual({ estado: 'sincronizada', sincronizada_em: '2026-11-16T12:00:00.000Z' });
  });

  it('visita recusada guarda o motivo e só ela pode ser descartada', async () => {
    await visitas.registrar(visita('recusada'));
    await visitas.registrar(visita('pendente'));
    await visitas.marcarRejeitada('recusada', 'Data no futuro');

    expect((await visitas.rejeitadas())[0]).toMatchObject({ id: 'recusada', motivoRejeicao: 'Data no futuro' });

    await visitas.descartarRejeitada('pendente'); // não é recusada: deve continuar lá
    await visitas.descartarRejeitada('recusada');
    expect(await visitas.rejeitadas()).toHaveLength(0);
    expect(await visitas.contarPendentes()).toBe(1);
  });

  it('casasComPendencia lista cada casa uma vez e ignora as já enviadas', async () => {
    await visitas.registrar(visita('a', { domicilioId: 'casa-1' }));
    await visitas.registrar(visita('b', { domicilioId: 'casa-1' }));
    await visitas.registrar(visita('c', { domicilioId: 'casa-2' }));
    await visitas.marcarSincronizada('c', new Date());
    expect([...(await visitas.casasComPendencia())]).toEqual(['casa-1']);
  });
});

describe('CacheLocal', () => {
  it('guarda e devolve o objeto com o momento em que foi salvo', async () => {
    const cache = new CacheLocal(banco);
    await cache.salvar('fila', { casas: [1, 2] }, new Date('2026-11-16T09:00:00Z'));
    expect(await cache.ler('fila')).toEqual({ valor: { casas: [1, 2] }, salvoEm: '2026-11-16T09:00:00.000Z' });
  });

  it('salvar de novo na mesma chave substitui (não acumula)', async () => {
    const cache = new CacheLocal(banco);
    await cache.salvar('fila', { v: 1 });
    await cache.salvar('fila', { v: 2 });
    expect((await cache.ler<{ v: number }>('fila'))?.valor.v).toBe(2);
  });

  it('chave inexistente devolve null', async () => {
    expect(await new CacheLocal(banco).ler('nada')).toBeNull();
  });

  it('limpar apaga o cache (ao sair da conta) mas NÃO apaga visitas pendentes', async () => {
    const cache = new CacheLocal(banco);
    await cache.salvar('fila', { v: 1 });
    await visitas.registrar(visita('v1'));
    await cache.limpar();
    expect(await cache.ler('fila')).toBeNull();
    expect(await visitas.contarPendentes()).toBe(1);
  });
});

describe('Configuracao', () => {
  it('grava e lê o endereço do servidor', async () => {
    const config = new Configuracao(banco);
    expect(await config.ler('servidor')).toBeNull();
    await config.gravar('servidor', 'https://a.trycloudflare.com');
    await config.gravar('servidor', 'https://b.trycloudflare.com');
    expect(await config.ler('servidor')).toBe('https://b.trycloudflare.com');
  });
});
