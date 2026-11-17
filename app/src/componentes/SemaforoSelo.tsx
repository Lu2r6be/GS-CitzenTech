import { StyleSheet, Text, View } from 'react-native';

import type { Nivel } from '@/api/tipos';
import { aparenciaDe } from '@/util/semaforo';

interface Props {
  nivel: Nivel;
  grande?: boolean;
}

/** O semáforo: cor + palavra. Se a cor falhar (sol, daltonismo), a palavra ainda diz tudo. */
export function SemaforoSelo({ nivel, grande = false }: Props) {
  const a = aparenciaDe(nivel);
  return (
    <View
      accessible
      accessibilityLabel={`${a.rotulo}: ${a.explicacao}`}
      style={[estilos.selo, { backgroundColor: a.fundo }, grande && estilos.seloGrande]}
    >
      <Text style={[estilos.rotulo, { color: a.texto }, grande && estilos.rotuloGrande]}>{a.rotulo}</Text>
    </View>
  );
}

const estilos = StyleSheet.create({
  selo: { borderRadius: 12, paddingVertical: 14, paddingHorizontal: 10, alignItems: 'center', justifyContent: 'center', minWidth: 96 },
  seloGrande: { paddingVertical: 28, paddingHorizontal: 24, borderRadius: 20, alignSelf: 'stretch' },
  rotulo: { fontSize: 16, fontWeight: '800', textAlign: 'center' },
  rotuloGrande: { fontSize: 38 },
});
