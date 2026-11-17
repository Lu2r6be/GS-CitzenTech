import { DatabaseSync } from 'node:sqlite';

import type { BancoLocal, Valor } from '@/banco/BancoLocal';

/** Só para testes: SQLite de verdade, em memória, atrás do mesmo contrato que o app usa no celular. */
export function criarBancoEmMemoria(): BancoLocal {
  const db = new DatabaseSync(':memory:');
  return {
    script: async (sql) => {
      db.exec(sql);
    },
    executar: async (sql, parametros: Valor[] = []) => {
      db.prepare(sql).run(...parametros);
    },
    consultar: async <T>(sql: string, parametros: Valor[] = []) => db.prepare(sql).all(...parametros) as T[],
  };
}
