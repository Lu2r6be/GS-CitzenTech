import { StyleSheet, Text, TextInput, View, type TextInputProps } from 'react-native';

import { alturaMinimaDeToque, cores, fonte } from '@/tema/tema';

interface Props extends TextInputProps {
  rotulo: string;
  erro?: string;
  ajuda?: string;
}

/** Campo de texto com o rótulo SEMPRE visível (placeholder some ao digitar e o leitor de tela se perde). */
export function Campo({ rotulo, erro, ajuda, style, ...resto }: Props) {
  return (
    <View style={estilos.grupo}>
      <Text style={estilos.rotulo}>{rotulo}</Text>
      <TextInput
        accessibilityLabel={rotulo}
        accessibilityHint={ajuda}
        placeholderTextColor="#6B7580"
        style={[estilos.entrada, !!erro && estilos.entradaComErro, resto.multiline && estilos.multilinha, style]}
        {...resto}
      />
      {ajuda && !erro ? <Text style={estilos.ajuda}>{ajuda}</Text> : null}
      {erro ? (
        <Text style={estilos.erro} accessibilityLiveRegion="polite">
          {erro}
        </Text>
      ) : null}
    </View>
  );
}

const estilos = StyleSheet.create({
  grupo: { gap: 6 },
  rotulo: { fontSize: fonte.corpo, fontWeight: '700', color: cores.texto },
  entrada: {
    minHeight: alturaMinimaDeToque,
    borderWidth: 2,
    borderColor: cores.borda,
    borderRadius: 12,
    paddingHorizontal: 14,
    fontSize: fonte.corpo,
    color: cores.texto,
    backgroundColor: cores.cartao,
  },
  entradaComErro: { borderColor: cores.perigo },
  multilinha: { minHeight: 110, paddingTop: 12, textAlignVertical: 'top' },
  ajuda: { fontSize: fonte.pequena, color: cores.textoSuave },
  erro: { fontSize: fonte.pequena, color: cores.erroTexto, fontWeight: '700' },
});
