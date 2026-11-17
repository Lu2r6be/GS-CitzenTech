/**
 * Teste de INTEGRAÇÃO: usa o cliente e o sincronizador do app contra a API de verdade.
 * Só roda se você informar o endereço da API (senão é ignorado):
 *
 *   API_URL=http://localhost:5080 npm test
 *
 * A API precisa estar no ar com o cenário de demonstração (api/README.md).
 */
import { randomUUID } from 'node:crypto';

import { beforeAll, describe, expect, it } from 'vitest';

import { prepararBanco } from '@/banco/esquema';
import { VisitasLocais } from '@/banco/repositorios';
import { criarSincronizador } from '@/sincronizacao/sincronizador';
import { criarBancoEmMemoria } from '@/testes/bancoEmMemoria';
import { cpfValido } from '@/util/cpf';

import { ClienteApi, ErroDaApi, SessaoExpirada } from './cliente';
import type { CadastroDeResidente } from './tipos';

const API_URL = process.env.API_URL;
const SENHA_DEMO = 'Demo@2026!';

function clienteComToken(token: string | null = null): ClienteApi {
  return new ClienteApi({ baseUrl: () => API_URL!, token: () => token });
}

async function entrar(email: string): Promise<ClienteApi> {
  const login = await clienteComToken().login(email, SENHA_DEMO);
  return clienteComToken(login.token);
}

/** Gera um CPF válido diferente a cada execução (a API recusa CPF repetido). */
function cpfNovo(): string {
  for (;;) {
    const base = Array.from({ length: 9 }, () => Math.floor(Math.random() * 10));
    const digito = (n: number) => {
      const soma = base.reduce((acc, d, i) => acc + d * (n + 1 - i), 0);
      const resto = (soma * 10) % 11;
      return resto === 10 ? 0 : resto;
    };
    base.push(digito(9));
    base.push(digito(10));
    const cpf = base.join('');
    if (cpfValido(cpf)) return cpf;
  }
}

describe.skipIf(!API_URL)('app x API real', () => {
  let acs: ClienteApi;
  let primeiraCasaId: string;

  beforeAll(async () => {
    acs = await entrar('ana.acs@demo.local');
  });

  it('login com senha errada: mensagem simples e nada de token', async () => {
    const erro = await clienteComToken().login('ana.acs@demo.local', 'senha-errada').catch((e) => e);
    expect(erro).toBeInstanceOf(ErroDaApi);
    expect(erro.status).toBe(401);
    expect(erro.message).toBe('E-mail ou senha incorretos.');
  });

  it('login válido devolve token, validade e papel', async () => {
    const login = await clienteComToken().login('ana.acs@demo.local', SENHA_DEMO);
    expect(login.papel).toBe('Acs');
    expect(login.token.split('.')).toHaveLength(3);
    expect(new Date(login.expiraEm).getTime()).toBeGreaterThan(Date.now());
  });

  it('sem token a fila responde 401 e o cliente lança SessaoExpirada', async () => {
    await expect(clienteComToken().fila()).rejects.toBeInstanceOf(SessaoExpirada);
  });

  it('fila: formato que o app espera, com cinza (sem sinal) antes dos demais', async () => {
    const fila = await acs.fila();
    expect(['nenhum', 'calor', 'frio']).toContain(fila.diaDeRisco);
    expect(fila.casas.length).toBeGreaterThanOrEqual(5);
    for (const casa of fila.casas) {
      expect(casa).toHaveProperty('domicilioId');
      expect(['azul', 'verde', 'amarelo', 'vermelho', 'cinza']).toContain(casa.nivel);
      expect(typeof casa.semSinal).toBe('boolean');
      expect(typeof casa.pontosDeVulnerabilidade).toBe('number');
      expect(casa.nivel === 'cinza').toBe(casa.semSinal);
    }
    const primeiroNaoCinza = fila.casas.findIndex((c) => c.nivel !== 'cinza');
    if (primeiroNaoCinza > 0) {
      expect(fila.casas.slice(0, primeiroNaoCinza).every((c) => c.nivel === 'cinza')).toBe(true);
    }
    primeiraCasaId = fila.casas[0].domicilioId;
  });

  it('detalhe da casa: CPF sempre mascarado e lista de leituras', async () => {
    const casa = await acs.casa(primeiraCasaId);
    expect(casa.id).toBe(primeiraCasaId);
    expect(casa.cpfMascarado).toMatch(/\*/);
    expect(casa.cpfMascarado).not.toMatch(/^\d{3}\.\d{3}\.\d{3}-\d{2}$/);
    expect(Array.isArray(casa.ultimasLeituras)).toBe(true);
    expect(['subindo', 'estavel', 'descendo']).toContain(casa.tendencia);
  });

  it('visita feita offline: sobe pelo sincronizador, e reenviar o mesmo id não duplica', async () => {
    const visitaId = randomUUID();
    const registro = {
      id: visitaId,
      domicilioId: primeiraCasaId,
      realizadaEm: new Date().toISOString(),
      resultado: 'orientado' as const,
      observacao: 'Orientada a beber água',
    };

    const banco1 = criarBancoEmMemoria();
    await prepararBanco(banco1);
    const visitas1 = new VisitasLocais(banco1);
    await visitas1.registrar(registro);
    const resumo1 = await criarSincronizador(acs, visitas1)();
    expect(resumo1).toMatchObject({ enviadas: 1, rejeitadas: 0, restantes: 0, semRede: false });

    // "Celular reiniciado": outro banco local tenta mandar a MESMA visita de novo.
    const banco2 = criarBancoEmMemoria();
    await prepararBanco(banco2);
    const visitas2 = new VisitasLocais(banco2);
    await visitas2.registrar(registro);
    const resumo2 = await criarSincronizador(acs, visitas2)();
    expect(resumo2).toMatchObject({ enviadas: 0, jaExistiam: 1, restantes: 0 });

    const fila = await acs.fila();
    expect(fila.casas.find((c) => c.domicilioId === primeiraCasaId)?.ultimaVisitaEm).not.toBeNull();
  });

  it('visita com data no futuro é recusada com o motivo, sem derrubar o lote', async () => {
    const banco = criarBancoEmMemoria();
    await prepararBanco(banco);
    const visitas = new VisitasLocais(banco);
    const futura = new Date(Date.now() + 24 * 3_600_000).toISOString();
    await visitas.registrar({ id: randomUUID(), domicilioId: primeiraCasaId, realizadaEm: futura, resultado: 'tudoBem', observacao: null });
    await visitas.registrar({ id: randomUUID(), domicilioId: primeiraCasaId, realizadaEm: new Date().toISOString(), resultado: 'tudoBem', observacao: null });

    const resumo = await criarSincronizador(acs, visitas)();
    expect(resumo).toMatchObject({ enviadas: 1, rejeitadas: 1, restantes: 0 });
    const [recusada] = await visitas.rejeitadas();
    expect(recusada.motivoRejeicao).toMatch(/futuro/i);
  });

  it('cadastro sem consentimento é recusado (400) e com consentimento entra na fila como cinza', async () => {
    const pedido: CadastroDeResidente = {
      nome: 'Residente de Teste',
      cpf: cpfNovo(),
      dataNascimento: '1950-05-10',
      moraSozinho: true,
      temDoencaCronica: false,
      condicoesSaude: null,
      telefoneContato: null,
      endereco: 'Rua do Teste, 1',
      cep: '01310100',
      tipoCobertura: 'fibrocimento',
      andar: null,
      microareaId: null,
      consentimentoConcedido: false,
    };

    const recusado = await acs.cadastrarResidente(pedido).catch((e) => e);
    expect(recusado).toBeInstanceOf(ErroDaApi);
    expect(recusado.status).toBe(400);
    expect(recusado.message).toMatch(/consentimento/i);

    const criado = await acs.cadastrarResidente({ ...pedido, consentimentoConcedido: true });
    expect(criado.cpfMascarado).toMatch(/\*/);

    const fila = await acs.fila();
    const nova = fila.casas.find((c) => c.domicilioId === criado.domicilioId);
    expect(nova?.nivel).toBe('cinza');
  });

  it('familiar vê só a casa dele, em formato simples, e não acessa a fila (403)', async () => {
    const familiar = await entrar('neto@demo.local');
    const casa = await familiar.casaDoFamiliar();
    expect(['azul', 'verde', 'amarelo', 'vermelho', 'cinza']).toContain(casa.nivel);
    expect(casa.mensagem.length).toBeGreaterThan(10);

    const erro = await familiar.fila().catch((e) => e);
    expect(erro).toBeInstanceOf(ErroDaApi);
    expect(erro.status).toBe(403);
  });
});
