import { Pressable, StyleSheet, Text, View } from 'react-native';

import { alturaMinimaDeToque, cores, fonte } from '@/tema/tema';

interface Opcao<T extends string> {
  valor: T;
  titulo: string;
  dica?: string;
}

interface Props<T extends string> {
  opcoes: Opcao<T>[];
  valor: T | null;
  aoMudar: (valor: T) => void;
}

/** Lista de botões grandes em que só um pode ficar marcado (o leitor de tela anuncia "marcado"). */
export function EscolhaUnica<T extends string>({ opcoes, valor, aoMudar }: Props<T>) {
  return (
    <View style={estilos.lista} accessibilityRole="radiogroup">
      {opcoes.map((opcao) => {
        const marcado = opcao.valor === valor;
        return (
          <Pressable
            key={opcao.valor}
            accessibilityRole="radio"
            accessibilityLabel={opcao.titulo}
            accessibilityHint={opcao.dica}
            accessibilityState={{ checked: marcado }}
            onPress={() => aoMudar(opcao.valor)}
            style={[estilos.opcao, marcado && estilos.opcaoMarcada]}
          >
            <View style={[estilos.bolinha, marcado && estilos.bolinhaMarcada]}>
              {marcado ? <View style={estilos.miolo} /> : null}
            </View>
            <View style={estilos.textos}>
              <Text style={estilos.titulo}>{opcao.titulo}</Text>
              {opcao.dica ? <Text style={estilos.dica}>{opcao.dica}</Text> : null}
            </View>
          </Pressable>
        );
      })}
    </View>
  );
}

const estilos = StyleSheet.create({
  lista: { gap: 10 },
  opcao: {
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
  opcaoMarcada: { borderColor: cores.primaria, backgroundColor: cores.infoFundo },
  bolinha: { width: 26, height: 26, borderRadius: 13, borderWidth: 2, borderColor: cores.textoSuave, alignItems: 'center', justifyContent: 'center' },
  bolinhaMarcada: { borderColor: cores.primaria },
  miolo: { width: 12, height: 12, borderRadius: 6, backgroundColor: cores.primaria },
  textos: { flex: 1, gap: 2 },
  titulo: { fontSize: fonte.corpo, fontWeight: '700', color: cores.texto },
  dica: { fontSize: fonte.pequena, color: cores.textoSuave },
});
