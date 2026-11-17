import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import { BotaoSair } from '@/componentes/BotaoSair';
import { AppProvider, useApp } from '@/contexto/AppContexto';
import { cores } from '@/tema/tema';

/**
 * Quem pode ver o quê: cada grupo "Protected" só existe para o papel certo.
 * Sem login só aparece a tela de login; ACS vê fila/casa/visita/cadastro; familiar vê só a casa dele.
 */
function Rotas() {
  const { sessao } = useApp();
  const papel = sessao?.papel;

  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: cores.cartao },
        headerTintColor: cores.texto,
        headerTitleStyle: { fontWeight: '800' },
        contentStyle: { backgroundColor: cores.fundo },
        headerBackTitle: 'Voltar',
      }}
    >
      <Stack.Screen name="index" options={{ headerShown: false }} />

      <Stack.Protected guard={!sessao}>
        <Stack.Screen name="login" options={{ headerShown: false }} />
      </Stack.Protected>

      <Stack.Protected guard={papel === 'Acs'}>
        <Stack.Screen name="fila" options={{ title: 'Fila de visitas', headerRight: () => <BotaoSair /> }} />
        <Stack.Screen name="casa/[id]" options={{ title: 'Casa' }} />
        <Stack.Screen name="visita/[id]" options={{ title: 'Registrar visita' }} />
        <Stack.Screen name="cadastro" options={{ title: 'Cadastrar residente' }} />
      </Stack.Protected>

      <Stack.Protected guard={papel === 'Familiar'}>
        <Stack.Screen name="familiar" options={{ title: 'Minha casa', headerRight: () => <BotaoSair /> }} />
      </Stack.Protected>

      <Stack.Protected guard={papel === 'Admin'}>
        <Stack.Screen name="sem-acesso" options={{ title: 'Administrador', headerRight: () => <BotaoSair /> }} />
      </Stack.Protected>
    </Stack>
  );
}

export default function LayoutRaiz() {
  return (
    <AppProvider>
      <StatusBar style="dark" />
      <Rotas />
    </AppProvider>
  );
}
