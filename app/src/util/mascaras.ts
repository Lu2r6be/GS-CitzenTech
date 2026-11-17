import { soDigitos } from './cpf';

/** 01310-100 enquanto digita. */
export function mascararCep(texto: string): string {
  const d = soDigitos(texto).slice(0, 8);
  return d.length > 5 ? `${d.slice(0, 5)}-${d.slice(5)}` : d;
}

/** (11) 98765-4321 enquanto digita (aceita fixo, com 10 números, e celular, com 11). */
export function mascararTelefone(texto: string): string {
  const d = soDigitos(texto).slice(0, 11);
  if (d.length <= 2) return d;
  const ddd = `(${d.slice(0, 2)}) `;
  const resto = d.slice(2);
  if (resto.length <= 4) return ddd + resto;
  const corte = d.length === 11 ? 5 : 4;
  return `${ddd}${resto.slice(0, corte)}-${resto.slice(corte)}`;
}
