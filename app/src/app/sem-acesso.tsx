import { StyleSheet, Text, View } from 'react-native';

import { cores, fonte } from '@/tema/tema';

export default function SemAcesso() {
  return (
    <View style={estilos.caixa}>
      <Text style={estilos.titulo} accessibilityRole="header">
        Perfil de administrador
      </Text>
      <Text style={estilos.texto}>
        Este aplicativo é para agentes de saúde (fila de visitas) e familiares (status da casa). O administrador usa a
        documentação da API (Swagger) e o painel de segurança.
      </Text>
    </View>
  );
}

const estilos = StyleSheet.create({
  caixa: { flex: 1, padding: 24, gap: 12, justifyContent: 'center', backgroundColor: cores.fundo },
  titulo: { fontSize: fonte.titulo, fontWeight: '800', color: cores.texto },
  texto: { fontSize: fonte.corpo, color: cores.textoSuave },
});
