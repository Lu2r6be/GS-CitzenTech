import { describe, expect, it } from 'vitest';

import { contemCpf, cpfValido, mascararCpfDigitando } from './cpf';
import { dataBrParaIso, haQuantoTempo, mascararDataDigitando } from './datas';
import { formularioParaPedido, FORMULARIO_VAZIO, validarCadastro, type FormularioDeCadastro } from './cadastro';
import { mascararCep, mascararTelefone } from './mascaras';
import { aparenciaDe, APARENCIA, avisoDoDia, textoDaTendencia } from './semaforo';

const HOJE = new Date('2026-11-16T12:00:00Z');

describe('CPF', () => {
  it('aceita CPF válido, com ou sem pontuação', () => {
    expect(cpfValido('529.982.247-25')).toBe(true);
    expect(cpfValido('52998224725')).toBe(true);
  });

  it('recusa dígito errado, tamanho errado e sequência repetida', () => {
    expect(cpfValido('529.982.247-24')).toBe(false);
    expect(cpfValido('123')).toBe(false);
    expect(cpfValido('111.111.111-11')).toBe(false);
  });

  it('máscara aparece enquanto digita', () => {
    expect(mascararCpfDigitando('5299')).toBe('529.9');
    expect(mascararCpfDigitando('52998224725')).toBe('529.982.247-25');
    expect(mascararCpfDigitando('529982247259999')).toBe('529.982.247-25');
  });

  it('acha CPF válido escondido em texto livre, mas não um número qualquer', () => {
    expect(contemCpf('Paciente CPF 529.982.247-25 estava bem')).toBe(true);
    expect(contemCpf('cpf 52998224725')).toBe(true);
    expect(contemCpf('Ligar para 11 98765-4321 depois das 14h')).toBe(false);
    expect(contemCpf('Estava bem, bebendo água')).toBe(false);
  });
});

describe('datas', () => {
  it('converte DD/MM/AAAA para AAAA-MM-DD', () => {
    expect(dataBrParaIso('25/03/1948', HOJE)).toBe('1948-03-25');
  });

  it('recusa data inexistente, futura ou mal escrita', () => {
    expect(dataBrParaIso('31/02/1950', HOJE)).toBeNull();
    expect(dataBrParaIso('01/01/2030', HOJE)).toBeNull();
    expect(dataBrParaIso('1948-03-25', HOJE)).toBeNull();
    expect(dataBrParaIso('', HOJE)).toBeNull();
  });

  it('máscara de data', () => {
    expect(mascararDataDigitando('25031948')).toBe('25/03/1948');
    expect(mascararDataDigitando('2503')).toBe('25/03');
  });

  it('haQuantoTempo em palavras simples', () => {
    const agora = new Date('2026-11-16T12:00:00Z');
    expect(haQuantoTempo(null, agora)).toBe('nunca');
    expect(haQuantoTempo('2026-11-16T11:59:40Z', agora)).toBe('agora há pouco');
    expect(haQuantoTempo('2026-11-16T11:30:00Z', agora)).toBe('há 30 min');
    expect(haQuantoTempo('2026-11-16T09:00:00Z', agora)).toBe('há 3 h');
    expect(haQuantoTempo('2026-11-13T12:00:00Z', agora)).toBe('há 3 dias');
  });
});

describe('máscaras', () => {
  it('CEP e telefone', () => {
    expect(mascararCep('01310100')).toBe('01310-100');
    expect(mascararTelefone('11987654321')).toBe('(11) 98765-4321');
    expect(mascararTelefone('1133334444')).toBe('(11) 3333-4444');
  });
});

describe('semáforo', () => {
  it('todo nível tem palavra e explicação (a cor nunca é a única pista)', () => {
    for (const nivel of ['azul', 'verde', 'amarelo', 'vermelho', 'cinza'] as const) {
      const a = APARENCIA[nivel];
      expect(a.rotulo.length).toBeGreaterThan(0);
      expect(a.explicacao.length).toBeGreaterThan(0);
    }
  });

  it('nível desconhecido cai em "sem sinal" em vez de quebrar a tela', () => {
    expect(aparenciaDe('roxo' as never).rotulo).toBe('Sem sinal');
  });

  it('contraste de cada selo é de pelo menos 4,5:1 (WCAG AA)', () => {
    const luz = (hex: string) => {
      const c = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
      return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
    };
    for (const a of Object.values(APARENCIA)) {
      const [claro, escuro] = [luz(a.fundo), luz(a.texto)].sort((x, y) => y - x);
      expect((claro + 0.05) / (escuro + 0.05)).toBeGreaterThanOrEqual(4.5);
    }
  });

  it('textos do app não usam emoji nem setas (decisão de interface)', () => {
    const textos = [
      ...Object.values(APARENCIA).map((a) => a.rotulo + a.explicacao),
      textoDaTendencia('subindo'),
      textoDaTendencia('descendo'),
      textoDaTendencia('estavel'),
    ].join('');
    expect(textos).toMatch(/^[\p{L}\p{Zs},.]+$/u);
  });

  it('aviso do dia só aparece em dia de risco', () => {
    expect(avisoDoDia('nenhum')).toBeNull();
    expect(avisoDoDia('calor')).toMatch(/calor/);
    expect(avisoDoDia('frio')).toMatch(/frio/);
  });
});

describe('cadastro de residente', () => {
  const completo: FormularioDeCadastro = {
    ...FORMULARIO_VAZIO,
    nome: 'Maria Teste',
    cpf: '529.982.247-25',
    dataNascimento: '25/03/1948',
    endereco: 'Rua das Flores, 10',
    cep: '01310-100',
    consentimento: true,
  };

  it('formulário completo não tem erros', () => {
    expect(validarCadastro(completo, HOJE)).toEqual({});
  });

  it('formulário vazio aponta os campos obrigatórios, inclusive o consentimento', () => {
    const erros = validarCadastro(FORMULARIO_VAZIO, HOJE);
    expect(Object.keys(erros).sort()).toEqual(['cep', 'consentimento', 'cpf', 'dataNascimento', 'endereco', 'nome']);
  });

  it('sem consentimento NÃO cadastra (LGPD)', () => {
    expect(validarCadastro({ ...completo, consentimento: false }, HOJE).consentimento).toBeDefined();
  });

  it('o pedido leva só números no CPF/CEP e a data em formato ISO', () => {
    const pedido = formularioParaPedido({ ...completo, telefone: '(11) 98765-4321', andar: '3' }, HOJE);
    expect(pedido).toMatchObject({
      cpf: '52998224725',
      cep: '01310100',
      dataNascimento: '1948-03-25',
      telefoneContato: '11987654321',
      andar: 3,
      microareaId: null,
      consentimentoConcedido: true,
    });
  });

  it('condições de saúde só vão se marcou "doença crônica"', () => {
    const sem = formularioParaPedido({ ...completo, condicoesSaude: 'diabetes', temDoencaCronica: false }, HOJE);
    expect(sem.condicoesSaude).toBeNull();
    const com = formularioParaPedido({ ...completo, condicoesSaude: ' diabetes ', temDoencaCronica: true }, HOJE);
    expect(com.condicoesSaude).toBe('diabetes');
  });
});
