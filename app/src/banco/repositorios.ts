import type { ResultadoVisita } from '@/api/tipos';

import type { BancoLocal } from './BancoLocal';

export type EstadoDaVisita = 'pendente' | 'sincronizada' | 'rejeitada';

export interface VisitaLocal {
  id: string;
  domicilioId: string;
  realizadaEm: string;
  resultado: ResultadoVisita;
  observacao: string | null;
  estado: EstadoDaVisita;
  motivoRejeicao: string | null;
}

interface LinhaDeVisita {
  id: string;
  domicilio_id: string;
  realizada_em: string;
  resultado: ResultadoVisita;
  observacao: string | null;
  estado: EstadoDaVisita;
  motivo_rejeicao: string | null;
}

const COLUNAS = 'id, domicilio_id, realizada_em, resultado, observacao, estado, motivo_rejeicao';

function paraVisita(linha: LinhaDeVisita): VisitaLocal {
  return {
    id: linha.id,
    domicilioId: linha.domicilio_id,
    realizadaEm: linha.realizada_em,
    resultado: linha.resultado,
    observacao: linha.observacao,
    estado: linha.estado,
    motivoRejeicao: linha.motivo_rejeicao,
  };
}

/** As visitas guardadas no celular (a "caixa de saída" do ACS). */
export class VisitasLocais {
  constructor(private readonly banco: BancoLocal) {}

  async registrar(visita: {
    id: string;
    domicilioId: string;
    realizadaEm: string;
    resultado: ResultadoVisita;
    observacao: string | null;
  }): Promise<void> {
    await this.banco.executar(
      `INSERT INTO visita_local (id, domicilio_id, realizada_em, resultado, observacao, estado, criada_em)
       VALUES (?, ?, ?, ?, ?, 'pendente', ?)`,
      [visita.id, visita.domicilioId, visita.realizadaEm, visita.resultado, visita.observacao, new Date().toISOString()],
    );
  }

  /** Mais antigas primeiro: a ordem em que o ACS as fez. */
  async pendentes(): Promise<VisitaLocal[]> {
    const linhas = await this.banco.consultar<LinhaDeVisita>(
      `SELECT ${COLUNAS} FROM visita_local WHERE estado = 'pendente' ORDER BY realizada_em ASC`,
    );
    return linhas.map(paraVisita);
  }

  async rejeitadas(): Promise<VisitaLocal[]> {
    const linhas = await this.banco.consultar<LinhaDeVisita>(
      `SELECT ${COLUNAS} FROM visita_local WHERE estado = 'rejeitada' ORDER BY realizada_em ASC`,
    );
    return linhas.map(paraVisita);
  }

  async contarPendentes(): Promise<number> {
    const [linha] = await this.banco.consultar<{ total: number }>(
      `SELECT COUNT(*) AS total FROM visita_local WHERE estado = 'pendente'`,
    );
    return linha?.total ?? 0;
  }

  /** Ids das casas que têm visita ainda não enviada (a fila mostra um aviso nelas). */
  async casasComPendencia(): Promise<Set<string>> {
    const linhas = await this.banco.consultar<{ domicilio_id: string }>(
      `SELECT DISTINCT domicilio_id FROM visita_local WHERE estado = 'pendente'`,
    );
    return new Set(linhas.map((l) => l.domicilio_id));
  }

  async marcarSincronizada(id: string, quando: Date): Promise<void> {
    await this.banco.executar(
      `UPDATE visita_local SET estado = 'sincronizada', motivo_rejeicao = NULL, sincronizada_em = ? WHERE id = ?`,
      [quando.toISOString(), id],
    );
  }

  async marcarRejeitada(id: string, motivo: string): Promise<void> {
    await this.banco.executar(
      `UPDATE visita_local SET estado = 'rejeitada', motivo_rejeicao = ? WHERE id = ?`,
      [motivo, id],
    );
  }

  /** Só visitas recusadas podem ser descartadas: pendente nunca se perde sem a pessoa saber. */
  async descartarRejeitada(id: string): Promise<void> {
    await this.banco.executar(`DELETE FROM visita_local WHERE id = ? AND estado = 'rejeitada'`, [id]);
  }
}

export interface ItemDeCache<T> {
  valor: T;
  salvoEm: string;
}

/** Última resposta boa de cada tela, para continuar útil sem internet. */
export class CacheLocal {
  constructor(private readonly banco: BancoLocal) {}

  async salvar(chave: string, valor: unknown, agora: Date = new Date()): Promise<void> {
    await this.banco.executar(
      `INSERT INTO cache (chave, valor, salvo_em) VALUES (?, ?, ?)
       ON CONFLICT(chave) DO UPDATE SET valor = excluded.valor, salvo_em = excluded.salvo_em`,
      [chave, JSON.stringify(valor), agora.toISOString()],
    );
  }

  async ler<T>(chave: string): Promise<ItemDeCache<T> | null> {
    const [linha] = await this.banco.consultar<{ valor: string; salvo_em: string }>(
      'SELECT valor, salvo_em FROM cache WHERE chave = ?',
      [chave],
    );
    if (!linha) return null;
    try {
      return { valor: JSON.parse(linha.valor) as T, salvoEm: linha.salvo_em };
    } catch {
      return null;
    }
  }

  /** Ao sair da conta: nome e endereço de moradores não ficam no aparelho de quem não está logado. */
  async limpar(): Promise<void> {
    await this.banco.executar('DELETE FROM cache');
  }
}

export class Configuracao {
  constructor(private readonly banco: BancoLocal) {}

  async ler(chave: string): Promise<string | null> {
    const [linha] = await this.banco.consultar<{ valor: string }>('SELECT valor FROM configuracao WHERE chave = ?', [chave]);
    return linha?.valor ?? null;
  }

  async gravar(chave: string, valor: string): Promise<void> {
    await this.banco.executar(
      `INSERT INTO configuracao (chave, valor) VALUES (?, ?)
       ON CONFLICT(chave) DO UPDATE SET valor = excluded.valor`,
      [chave, valor],
    );
  }
}
