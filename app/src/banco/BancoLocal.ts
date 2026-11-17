export type Valor = string | number | null;

/**
 * O pedaço mínimo de banco que o app precisa. No celular quem cumpre este contrato é o
 * expo-sqlite (bancoExpo.ts); nos testes, o SQLite embutido do Node. Assim o mesmo SQL
 * é testado de verdade sem abrir um celular.
 */
export interface BancoLocal {
  /** Roda vários comandos de uma vez (criação de tabelas). */
  script(sql: string): Promise<void>;
  executar(sql: string, parametros?: Valor[]): Promise<void>;
  consultar<T>(sql: string, parametros?: Valor[]): Promise<T[]>;
}
