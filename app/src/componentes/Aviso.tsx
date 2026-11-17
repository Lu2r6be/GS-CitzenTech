import { StyleSheet, Text, View } from 'react-native';

import { cores, fonte } from '@/tema/tema';

type Tom = 'info' | 'atencao' | 'erro';

const PALETA: Record<Tom, { fundo: string; texto: string }> = {
  info: { fundo: cores.infoFundo, texto: cores.infoTexto },
  atencao: { fundo: cores.avisoFundo, texto: cores.avisoTexto },
  erro: { fundo: cores.erroFundo, texto: cores.erroTexto },
};

/** Faixa de aviso (sem internet, visitas guardadas, dia de calor...). Lida em voz alta quando aparece. */
export function Aviso({ tom, texto, children }: { tom: Tom; texto: string; children?: React.ReactNode }) {
  const { fundo, texto: corTexto } = PALETA[tom];
  return (
    <View style={[estilos.caixa, { backgroundColor: fundo }]} accessible accessibilityLiveRegion="polite" accessibilityLabel={texto}>
      <Text style={[estilos.texto, { color: corTexto }]}>{texto}</Text>
      {children}
    </View>
  );
}

const estilos = StyleSheet.create({
  caixa: { borderRadius: 12, padding: 14, gap: 10 },
  texto: { fontSize: fonte.pequena, fontWeight: '600' },
});
