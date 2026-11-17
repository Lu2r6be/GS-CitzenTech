import type { DiaDeRisco, Nivel, ResultadoVisita, Tendencia } from '@/api/tipos';

export interface AparenciaDoNivel {
  /** Palavra curta: a cor nunca é a única pista (daltonismo, sol forte na tela). */
  rotulo: string;
  /** Uma frase para o leitor de tela e para quem quer entender na hora. */
  explicacao: string;
  fundo: string;
  texto: string;
}

// Todas as combinações fundo/texto têm contraste acima de 4,5:1 (recomendação WCAG AA).
export const APARENCIA: Record<Nivel, AparenciaDoNivel> = {
  vermelho: { rotulo: 'Perigo', explicacao: 'casa muito quente', fundo: '#C62828', texto: '#FFFFFF' },
  amarelo: { rotulo: 'Atenção', explicacao: 'casa quente', fundo: '#F2C200', texto: '#1A1A1A' },
  verde: { rotulo: 'Tudo certo', explicacao: 'temperatura confortável', fundo: '#1B7F3B', texto: '#FFFFFF' },
  azul: { rotulo: 'Frio', explicacao: 'casa fria', fundo: '#1565C0', texto: '#FFFFFF' },
  cinza: { rotulo: 'Sem sinal', explicacao: 'sensor sem resposta, vale ligar', fundo: '#5F6B76', texto: '#FFFFFF' },
};

export function aparenciaDe(nivel: Nivel): AparenciaDoNivel {
  return APARENCIA[nivel] ?? APARENCIA.cinza;
}

export function textoDaTendencia(tendencia: Tendencia): string {
  switch (tendencia) {
    case 'subindo':
      return 'Esquentando';
    case 'descendo':
      return 'Esfriando';
    default:
      return 'Estável';
  }
}

export function avisoDoDia(dia: DiaDeRisco): string | null {
  if (dia === 'calor') return 'Hoje é dia de calor forte. Casas quentes ficam mais perigosas.';
  if (dia === 'frio') return 'Hoje é dia de frio forte. Casas frias ficam mais perigosas.';
  return null;
}

export const OPCOES_DE_RESULTADO: { valor: ResultadoVisita; titulo: string; dica: string }[] = [
  { valor: 'tudoBem', titulo: 'Tudo bem', dica: 'A pessoa está bem e a casa está segura.' },
  { valor: 'orientado', titulo: 'Orientei a pessoa', dica: 'Conversei e dei orientações (água, ventilação, agasalho).' },
  { valor: 'encaminhadoUbs', titulo: 'Encaminhei para a UBS', dica: 'A pessoa precisa de atendimento na unidade de saúde.' },
  { valor: 'naoEncontrado', titulo: 'Não encontrei ninguém', dica: 'Fui até a casa e não consegui falar com a pessoa.' },
];

export function tituloDoResultado(resultado: ResultadoVisita): string {
  return OPCOES_DE_RESULTADO.find((o) => o.valor === resultado)?.titulo ?? resultado;
}
