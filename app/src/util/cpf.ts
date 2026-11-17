/** Deixa só os números. */
export function soDigitos(texto: string): string {
  return texto.replace(/\D/g, '');
}

/**
 * CPF válido = 11 dígitos, não todos iguais, e os dois últimos batem com o cálculo
 * dos dígitos verificadores (mesma regra da API).
 */
export function cpfValido(texto: string): boolean {
  const digitos = soDigitos(texto);
  if (digitos.length !== 11 || /^(\d)\1{10}$/.test(digitos)) return false;

  const calcular = (quantos: number): number => {
    let soma = 0;
    for (let i = 0; i < quantos; i += 1) soma += Number(digitos[i]) * (quantos + 1 - i);
    const resto = (soma * 10) % 11;
    return resto === 10 ? 0 : resto;
  };

  return calcular(9) === Number(digitos[9]) && calcular(10) === Number(digitos[10]);
}

/** Mostra 000.000.000-00 enquanto a pessoa digita. */
export function mascararCpfDigitando(texto: string): string {
  const d = soDigitos(texto).slice(0, 11);
  const partes = [d.slice(0, 3), d.slice(3, 6), d.slice(6, 9)].filter(Boolean);
  let saida = partes.join('.');
  if (d.length > 9) saida += `-${d.slice(9)}`;
  return saida;
}

/**
 * Procura um CPF válido escrito dentro de um texto livre (campo de observação).
 * Observação é texto livre: é por onde um dado pessoal "escapa" sem querer.
 */
export function contemCpf(texto: string): boolean {
  const candidatos = texto.match(/\d{3}[.\s]?\d{3}[.\s]?\d{3}[-\s]?\d{2}/g) ?? [];
  return candidatos.some(cpfValido);
}
