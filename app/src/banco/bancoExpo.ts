import { openDatabaseAsync } from 'expo-sqlite';

import type { BancoLocal, Valor } from './BancoLocal';

/** Abre (ou cria) o arquivo de banco do app no celular. */
export async function abrirBancoDoCelular(nomeDoArquivo = 'termometro-acs.db'): Promise<BancoLocal> {
  const db = await openDatabaseAsync(nomeDoArquivo);
  return {
    script: (sql) => db.execAsync(sql),
    executar: async (sql, parametros: Valor[] = []) => {
      await db.runAsync(sql, parametros);
    },
    consultar: <T>(sql: string, parametros: Valor[] = []) => db.getAllAsync<T>(sql, parametros),
  };
}
