import type { BancoLocal } from './BancoLocal';

/**
 * Tabelas do banco local (SQLite no celular):
 *  - visita_local: cada visita que o ACS registra. Nasce "pendente" e vira "sincronizada"
 *    quando o servidor confirma. É o que permite trabalhar sem internet.
 *  - cache: a última resposta da API (fila, detalhe da casa...) para mostrar algo sem rede.
 *  - configuracao: preferências do aparelho (ex.: endereço do servidor).
 *
 * "IF NOT EXISTS" deixa a função segura para rodar toda vez que o app abre.
 */
export const ESQUEMA = `
CREATE TABLE IF NOT EXISTS visita_local (
  id TEXT PRIMARY KEY,
  domicilio_id TEXT NOT NULL,
  realizada_em TEXT NOT NULL,
  resultado TEXT NOT NULL,
  observacao TEXT,
  estado TEXT NOT NULL DEFAULT 'pendente',
  motivo_rejeicao TEXT,
  criada_em TEXT NOT NULL,
  sincronizada_em TEXT
);
CREATE INDEX IF NOT EXISTS idx_visita_local_estado ON visita_local (estado);
CREATE TABLE IF NOT EXISTS cache (
  chave TEXT PRIMARY KEY,
  valor TEXT NOT NULL,
  salvo_em TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS configuracao (
  chave TEXT PRIMARY KEY,
  valor TEXT NOT NULL
);
`;

export function prepararBanco(banco: BancoLocal): Promise<void> {
  return banco.script(ESQUEMA);
}
