import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { useApp } from '@/contexto/AppContexto';
import { cores, fonte } from '@/tema/tema';
import { CONTA_ACS_DEMO, CONTA_FAMILIAR_DEMO, MODO_DEMO } from '@/util/demonstracao';

export default function Login() {
  const { entrar, servidor, trocarServidor } = useApp();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [entrando, setEntrando] = useState(false);
  const [editandoServidor, setEditandoServidor] = useState(false);
  const [novoServidor, setNovoServidor] = useState(servidor);

  async function aoEntrar() {
    if (!email.trim() || !senha) {
      setErro('Preencha o e-mail e a senha.');
      return;
    }
    setErro(null);
    setEntrando(true);
    try {
      await entrar(email, senha);
      // Tudo certo: o layout troca de tela sozinho quando a sessão existe.
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível entrar.');
      setEntrando(false);
    }
  }

  async function salvarServidor() {
    await trocarServidor(novoServidor);
    setEditandoServidor(false);
  }

  return (
    <SafeAreaView style={estilos.tela}>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
        <ScrollView contentContainerStyle={estilos.conteudo} keyboardShouldPersistTaps="handled">
          <View style={estilos.marca}>
            <Text style={estilos.titulo} accessibilityRole="header">
              Termômetro do ACS
            </Text>
            <Text style={estilos.subtitulo}>Mostra quais casas de idosos estão perigosas agora.</Text>
          </View>

          <Campo
            rotulo="E-mail"
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoCorrect={false}
            textContentType="username"
            autoComplete="email"
          />
          <Campo
            rotulo="Senha"
            value={senha}
            onChangeText={setSenha}
            secureTextEntry
            autoCapitalize="none"
            textContentType="password"
            autoComplete="password"
            onSubmitEditing={aoEntrar}
          />

          {erro ? <Aviso tom="erro" texto={erro} /> : null}

          <Botao titulo="Entrar" onPress={aoEntrar} carregando={entrando} />

          {MODO_DEMO ? (
            <View style={estilos.demo}>
              <Text style={estilos.demoTitulo}>Contas de demonstração (dados fictícios)</Text>
              <Botao
                titulo="Preencher como agente (ACS)"
                variante="secundario"
                onPress={() => {
                  setEmail(CONTA_ACS_DEMO.email);
                  setSenha(CONTA_ACS_DEMO.senha);
                }}
              />
              <Botao
                titulo="Preencher como familiar"
                variante="secundario"
                onPress={() => {
                  setEmail(CONTA_FAMILIAR_DEMO.email);
                  setSenha(CONTA_FAMILIAR_DEMO.senha);
                }}
              />
            </View>
          ) : null}

          <View style={estilos.servidor}>
            <Text style={estilos.servidorTexto}>Servidor: {servidor}</Text>
            {editandoServidor ? (
              <>
                <Campo
                  rotulo="Endereço do servidor"
                  value={novoServidor}
                  onChangeText={setNovoServidor}
                  autoCapitalize="none"
                  autoCorrect={false}
                  keyboardType="url"
                  ajuda="Ex.: https://meu-tunel.trycloudflare.com"
                />
                <Botao titulo="Salvar endereço" onPress={salvarServidor} />
              </>
            ) : (
              <Botao titulo="Mudar servidor" variante="secundario" onPress={() => setEditandoServidor(true)} />
            )}
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const estilos = StyleSheet.create({
  tela: { flex: 1, backgroundColor: cores.fundo },
  conteudo: { padding: 24, gap: 16 },
  marca: { alignItems: 'center', gap: 6, marginVertical: 16 },
  titulo: { fontSize: fonte.grande, fontWeight: '900', color: cores.texto },
  subtitulo: { fontSize: fonte.corpo, color: cores.textoSuave, textAlign: 'center' },
  demo: { gap: 10, marginTop: 8 },
  demoTitulo: { fontSize: fonte.pequena, fontWeight: '700', color: cores.textoSuave },
  servidor: { gap: 10, marginTop: 16 },
  servidorTexto: { fontSize: fonte.pequena, color: cores.textoSuave },
});
