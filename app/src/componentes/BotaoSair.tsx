import { Alert, Pressable, StyleSheet, Text } from 'react-native';

import { useApp } from '@/contexto/AppContexto';
import { cores } from '@/tema/tema';

/** "Sair" no canto do cabeçalho. Avisa se ainda há visitas esperando internet. */
export function BotaoSair() {
  const { sair, visitas } = useApp();

  async function perguntar() {
    const pendentes = await visitas.contarPendentes();
    const aviso =
      pendentes > 0
        ? `Há ${pendentes} visita(s) guardada(s) neste celular, ainda não enviadas. Elas não se perdem, mas só serão enviadas quando você entrar de novo com internet.`
        : 'Você vai precisar entrar de novo.';
    Alert.alert('Sair da conta?', aviso, [
      { text: 'Cancelar', style: 'cancel' },
      { text: 'Sair', style: 'destructive', onPress: () => void sair() },
    ]);
  }

  return (
    <Pressable accessibilityRole="button" accessibilityLabel="Sair da conta" onPress={perguntar} style={estilos.botao} hitSlop={8}>
      <Text style={estilos.texto}>Sair</Text>
    </Pressable>
  );
}

const estilos = StyleSheet.create({
  botao: { minHeight: 44, minWidth: 44, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 8 },
  texto: { color: cores.primaria, fontSize: 17, fontWeight: '700' },
});
