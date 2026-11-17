import { useLocalSearchParams, useRouter } from 'expo-router';
import { useRef, useState } from 'react';
import { Alert, KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text } from 'react-native';

import { SessaoExpirada } from '@/api/cliente';
import type { ResultadoVisita } from '@/api/tipos';
import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { EscolhaUnica } from '@/componentes/EscolhaUnica';
import { useApp } from '@/contexto/AppContexto';
import { cores, fonte } from '@/tema/tema';
import { contemCpf } from '@/util/cpf';
import { OPCOES_DE_RESULTADO } from '@/util/semaforo';
import { novoId } from '@/util/uuid';

const LIMITE_DA_OBSERVACAO = 500;

export default function RegistrarVisita() {
  const { id, nome } = useLocalSearchParams<{ id: string; nome?: string }>();
  const { visitas, sincronizar } = useApp();
  const router = useRouter();

  const [resultado, setResultado] = useState<ResultadoVisita | null>(null);
  const [observacao, setObservacao] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [salva, setSalva] = useState(false);

  // O id da visita nasce AQUI, uma vez só: se a pessoa tocar duas vezes ou o envio falhar,
  // continua sendo a mesma visita e a API nunca a conta em dobro.
  const visitaId = useRef(novoId());

  async function salvar() {
    if (!resultado) {
      setErro('Escolha o que aconteceu na visita.');
      return;
    }
    if (contemCpf(observacao)) {
      setErro('A observação parece ter um CPF. Apague o número: dados pessoais não devem ficar em texto livre.');
      return;
    }
    setErro(null);
    setSalvando(true);

    // 1) Guarda no celular PRIMEIRO: com ou sem internet, o trabalho do ACS não se perde.
    await visitas.registrar({
      id: visitaId.current,
      domicilioId: id,
      realizadaEm: new Date().toISOString(),
      resultado,
      observacao: observacao.trim() || null,
    });
    setSalva(true);

    // 2) Tenta enviar já. Se não der, fica pendente e a fila reenvia depois.
    let mensagem = 'Visita enviada ao servidor.';
    try {
      const resumo = await sincronizar();
      const recusada = (await visitas.rejeitadas()).find((v) => v.id === visitaId.current);
      if (recusada) {
        mensagem = `O servidor recusou esta visita: ${recusada.motivoRejeicao ?? 'sem motivo informado'}.`;
      } else if (resumo.semRede || resumo.restantes > 0) {
        mensagem = 'Sem internet: a visita ficou guardada no celular e será enviada assim que houver sinal.';
      }
    } catch (e) {
      if (e instanceof SessaoExpirada) return; // o app volta para o login sozinho; a visita continua guardada
      mensagem = 'A visita ficou guardada no celular. Não consegui enviar agora; vou tentar de novo.';
    }

    Alert.alert('Visita registrada', mensagem, [{ text: 'Voltar para a fila', onPress: () => router.replace('/fila') }]);
  }

  return (
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
      <ScrollView style={estilos.tela} contentContainerStyle={estilos.conteudo} keyboardShouldPersistTaps="handled">
        {nome ? (
          <Text style={estilos.titulo} accessibilityRole="header">
            Visita a {nome}
          </Text>
        ) : null}

        <Text style={estilos.pergunta}>O que aconteceu?</Text>
        <EscolhaUnica opcoes={OPCOES_DE_RESULTADO} valor={resultado} aoMudar={setResultado} />

        <Campo
          rotulo="Observação (opcional)"
          value={observacao}
          onChangeText={setObservacao}
          multiline
          maxLength={LIMITE_DA_OBSERVACAO}
          ajuda={`Não escreva CPF, senha nem detalhes de doença. ${observacao.length}/${LIMITE_DA_OBSERVACAO}`}
        />

        {erro ? <Aviso tom="erro" texto={erro} /> : null}

        <Botao titulo="Salvar visita" onPress={salvar} carregando={salvando} desabilitado={salva} />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const estilos = StyleSheet.create({
  tela: { flex: 1, backgroundColor: cores.fundo },
  conteudo: { padding: 16, gap: 16, paddingBottom: 32 },
  titulo: { fontSize: fonte.titulo, fontWeight: '800', color: cores.texto },
  pergunta: { fontSize: fonte.corpo + 1, fontWeight: '700', color: cores.texto },
});
