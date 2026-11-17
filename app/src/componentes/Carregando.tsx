import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

import { cores, fonte } from '@/tema/tema';

export function Carregando({ texto = 'Carregando...' }: { texto?: string }) {
  return (
    <View style={estilos.caixa} accessibilityRole="progressbar" accessibilityLabel={texto}>
      <ActivityIndicator size="large" color={cores.primaria} />
      <Text style={estilos.texto}>{texto}</Text>
    </View>
  );
}

const estilos = StyleSheet.create({
  caixa: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 12, backgroundColor: cores.fundo, padding: 24 },
  texto: { fontSize: fonte.corpo, color: cores.textoSuave },
});
