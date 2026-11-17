import { ActivityIndicator, Pressable, StyleSheet, Text } from 'react-native';

import { alturaMinimaDeToque, cores, fonte } from '@/tema/tema';

type Variante = 'primario' | 'secundario' | 'perigo';

interface Props {
  titulo: string;
  onPress: () => void;
  variante?: Variante;
  carregando?: boolean;
  desabilitado?: boolean;
  /** Frase extra para o leitor de tela explicar o que acontece ao tocar. */
  dica?: string;
}

export function Botao({ titulo, onPress, variante = 'primario', carregando = false, desabilitado = false, dica }: Props) {
  const inativo = desabilitado || carregando;
  const corDoTexto = variante === 'secundario' ? cores.primaria : cores.primariaTexto;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={titulo}
      accessibilityHint={dica}
      accessibilityState={{ disabled: inativo, busy: carregando }}
      disabled={inativo}
      onPress={onPress}
      style={({ pressed }) => [
        estilos.base,
        variante === 'primario' && estilos.primario,
        variante === 'secundario' && estilos.secundario,
        variante === 'perigo' && estilos.perigo,
        inativo && estilos.inativo,
        pressed && estilos.pressionado,
      ]}
    >
      {carregando ? (
        <ActivityIndicator color={corDoTexto} />
      ) : (
        <Text style={[estilos.texto, { color: corDoTexto }]}>{titulo}</Text>
      )}
    </Pressable>
  );
}

const estilos = StyleSheet.create({
  base: {
    minHeight: alturaMinimaDeToque,
    paddingHorizontal: 20,
    paddingVertical: 12,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  primario: { backgroundColor: cores.primaria },
  secundario: { backgroundColor: cores.cartao, borderWidth: 2, borderColor: cores.primaria },
  perigo: { backgroundColor: cores.perigo },
  inativo: { opacity: 0.5 },
  pressionado: { opacity: 0.8 },
  texto: { fontSize: fonte.corpo, fontWeight: '700', textAlign: 'center' },
});
