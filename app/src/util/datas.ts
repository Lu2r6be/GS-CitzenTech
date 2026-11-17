/** Texto curto "há quanto tempo" para a fila e para o detalhe. */
export function haQuantoTempo(iso: string | null, agora: Date = new Date()): string {
  if (!iso) return 'nunca';
  const minutos = Math.floor((agora.getTime() - new Date(iso).getTime()) / 60_000);
  if (minutos < 1) return 'agora há pouco';
  if (minutos < 60) return `há ${minutos} min`;
  const horas = Math.floor(minutos / 60);
  if (horas < 24) return `há ${horas} h`;
  const dias = Math.floor(horas / 24);
  return dias === 1 ? 'há 1 dia' : `há ${dias} dias`;
}

function doisDigitos(n: number): string {
  return String(n).padStart(2, '0');
}

/** 14:05 (horário do aparelho). */
export function formatarHora(iso: string): string {
  const d = new Date(iso);
  return `${doisDigitos(d.getHours())}:${doisDigitos(d.getMinutes())}`;
}

/** Mostra DD/MM/AAAA enquanto a pessoa digita. */
export function mascararDataDigitando(texto: string): string {
  const d = texto.replace(/\D/g, '').slice(0, 8);
  if (d.length <= 2) return d;
  if (d.length <= 4) return `${d.slice(0, 2)}/${d.slice(2)}`;
  return `${d.slice(0, 2)}/${d.slice(2, 4)}/${d.slice(4)}`;
}

/**
 * "25/03/1948" -> "1948-03-25". Devolve null se a data não existe (31/02) ou é do futuro.
 * `hoje` entra por parâmetro para o teste não depender do dia em que roda.
 */
export function dataBrParaIso(texto: string, hoje: Date = new Date()): string | null {
  const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(texto.trim());
  if (!m) return null;
  const dia = Number(m[1]);
  const mes = Number(m[2]);
  const ano = Number(m[3]);
  const data = new Date(Date.UTC(ano, mes - 1, dia));
  const existe = data.getUTCFullYear() === ano && data.getUTCMonth() === mes - 1 && data.getUTCDate() === dia;
  if (!existe || ano < 1900) return null;
  if (data.getTime() > hoje.getTime()) return null;
  return `${ano}-${doisDigitos(mes)}-${doisDigitos(dia)}`;
}
