import { StyleSheet, Text, View } from 'react-native';

import { cores, fonte } from '@/tema/tema';

import { Botao } from './Botao';

interface Props {
  mensagem: string;
  aoTentarDeNovo?: () => void;
}

/** Erro em tela cheia: diz o que houve em português simples e oferece tentar de novo. */
export function EstadoDeErro({ mensagem, aoTentarDeNovo }: Props) {
  return (
    <View style={estilos.caixa}>
      <Text style={estilos.titulo} accessibilityRole="header">
        Algo não deu certo
      </Text>
      <Text style={estilos.texto} accessibilityLiveRegion="polite">
        {mensagem}
      </Text>
      {aoTentarDeNovo ? <Botao titulo="Tentar de novo" onPress={aoTentarDeNovo} /> : null}
    </View>
  );
}

const estilos = StyleSheet.create({
  caixa: { flex: 1, alignItems: 'stretch', justifyContent: 'center', gap: 16, backgroundColor: cores.fundo, padding: 24 },
  titulo: { fontSize: fonte.titulo, fontWeight: '800', color: cores.texto, textAlign: 'center' },
  texto: { fontSize: fonte.corpo, color: cores.textoSuave, textAlign: 'center' },
});
