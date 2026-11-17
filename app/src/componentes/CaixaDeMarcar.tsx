import { Pressable, StyleSheet, Text, View } from 'react-native';

import { alturaMinimaDeToque, cores, fonte } from '@/tema/tema';

interface Props {
  texto: string;
  marcado: boolean;
  aoMudar: (marcado: boolean) => void;
  erro?: string;
}

export function CaixaDeMarcar({ texto, marcado, aoMudar, erro }: Props) {
  return (
    <View style={{ gap: 6 }}>
      <Pressable
        accessibilityRole="checkbox"
        accessibilityLabel={texto}
        accessibilityState={{ checked: marcado }}
        onPress={() => aoMudar(!marcado)}
        style={[estilos.linha, marcado && estilos.linhaMarcada]}
      >
        <View style={[estilos.quadrado, marcado && estilos.quadradoMarcado]}>
          {marcado ? <View style={estilos.marca} /> : null}
        </View>
        <Text style={estilos.texto}>{texto}</Text>
      </Pressable>
      {erro ? (
        <Text style={estilos.erro} accessibilityLiveRegion="polite">
          {erro}
        </Text>
      ) : null}
    </View>
  );
}

const estilos = StyleSheet.create({
  linha: {
    minHeight: alturaMinimaDeToque,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    padding: 14,
    borderRadius: 12,
    borderWidth: 2,
    borderColor: cores.borda,
    backgroundColor: cores.cartao,
  },
  linhaMarcada: { borderColor: cores.primaria, backgroundColor: cores.infoFundo },
  quadrado: {
    width: 28,
    height: 28,
    borderRadius: 6,
    borderWidth: 2,
    borderColor: cores.textoSuave,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: cores.cartao,
  },
  quadradoMarcado: { backgroundColor: cores.primaria, borderColor: cores.primaria },
  marca: { width: 8, height: 15, borderRightWidth: 3, borderBottomWidth: 3, borderColor: '#FFFFFF', transform: [{ rotate: '45deg' }, { translateY: -1 }] },
  texto: { flex: 1, fontSize: fonte.corpo, color: cores.texto },
  erro: { fontSize: fonte.pequena, color: cores.erroTexto, fontWeight: '700' },
});
