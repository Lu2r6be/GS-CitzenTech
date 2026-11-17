import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Alert, KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';

import { SessaoExpirada } from '@/api/cliente';
import type { TipoCobertura } from '@/api/tipos';
import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { CaixaDeMarcar } from '@/componentes/CaixaDeMarcar';
import { Campo } from '@/componentes/Campo';
import { EscolhaUnica } from '@/componentes/EscolhaUnica';
import { useApp } from '@/contexto/AppContexto';
import { cores, fonte } from '@/tema/tema';
import {
  FORMULARIO_VAZIO,
  formularioParaPedido,
  validarCadastro,
  type ErrosDoFormulario,
  type FormularioDeCadastro,
} from '@/util/cadastro';
import { mascararCpfDigitando } from '@/util/cpf';
import { mascararDataDigitando } from '@/util/datas';
import { mascararCep, mascararTelefone } from '@/util/mascaras';

const COBERTURAS: { valor: TipoCobertura; titulo: string; dica?: string }[] = [
  { valor: 'laje', titulo: 'Laje' },
  { valor: 'telha', titulo: 'Telha' },
  { valor: 'fibrocimento', titulo: 'Fibrocimento', dica: 'Esse telhado esquenta bastante.' },
  { valor: 'outro', titulo: 'Outro' },
];

/** Cadastro do residente + casa + consentimento (LGPD). Sem o aceite da pessoa, não envia. */
export default function Cadastro() {
  const { api } = useApp();
  const router = useRouter();
  const [form, setForm] = useState<FormularioDeCadastro>(FORMULARIO_VAZIO);
  const [erros, setErros] = useState<ErrosDoFormulario>({});
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  function mudar<K extends keyof FormularioDeCadastro>(campo: K, valor: FormularioDeCadastro[K]) {
    setForm((atual) => ({ ...atual, [campo]: valor }));
  }

  async function salvar() {
    const encontrados = validarCadastro(form);
    setErros(encontrados);
    setErroGeral(null);
    if (Object.keys(encontrados).length > 0) {
      setErroGeral('Tem campos para corrigir. Eles estão marcados abaixo.');
      return;
    }

    setEnviando(true);
    try {
      const criado = await api.cadastrarResidente(formularioParaPedido(form));
      Alert.alert('Residente cadastrado', `CPF registrado: ${criado.cpfMascarado}.`, [
        { text: 'Ver a fila', onPress: () => router.replace('/fila') },
      ]);
    } catch (e) {
      if (e instanceof SessaoExpirada) return;
      setErroGeral(e instanceof Error ? e.message : 'Não foi possível cadastrar.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1 }}>
      <ScrollView style={estilos.tela} contentContainerStyle={estilos.conteudo} keyboardShouldPersistTaps="handled">
        <Aviso tom="info" texto="O cadastro precisa de internet. Só as visitas funcionam sem sinal." />

        <Campo rotulo="Nome completo" value={form.nome} onChangeText={(t) => mudar('nome', t)} erro={erros.nome} autoCapitalize="words" />
        <Campo
          rotulo="CPF"
          value={form.cpf}
          onChangeText={(t) => mudar('cpf', mascararCpfDigitando(t))}
          erro={erros.cpf}
          keyboardType="number-pad"
          ajuda="Fica guardado só com os números e aparece sempre mascarado."
        />
        <Campo
          rotulo="Data de nascimento"
          value={form.dataNascimento}
          onChangeText={(t) => mudar('dataNascimento', mascararDataDigitando(t))}
          erro={erros.dataNascimento}
          keyboardType="number-pad"
          ajuda="Dia/mês/ano, como 25/03/1948"
        />
        <Campo rotulo="Telefone de contato (opcional)" value={form.telefone} onChangeText={(t) => mudar('telefone', mascararTelefone(t))} erro={erros.telefone} keyboardType="phone-pad" />

        <Campo rotulo="Endereço (rua e número)" value={form.endereco} onChangeText={(t) => mudar('endereco', t)} erro={erros.endereco} />
        <Campo rotulo="CEP" value={form.cep} onChangeText={(t) => mudar('cep', mascararCep(t))} erro={erros.cep} keyboardType="number-pad" />
        <Campo rotulo="Andar (se for apartamento)" value={form.andar} onChangeText={(t) => mudar('andar', t)} erro={erros.andar} keyboardType="number-pad" />

        <Text style={estilos.pergunta}>Tipo de telhado</Text>
        <EscolhaUnica opcoes={COBERTURAS} valor={form.tipoCobertura} aoMudar={(v) => mudar('tipoCobertura', v)} />

        <CaixaDeMarcar texto="A pessoa mora sozinha" marcado={form.moraSozinho} aoMudar={(v) => mudar('moraSozinho', v)} />
        <CaixaDeMarcar texto="Tem doença crônica" marcado={form.temDoencaCronica} aoMudar={(v) => mudar('temDoencaCronica', v)} />
        {form.temDoencaCronica ? (
          <Campo
            rotulo="Qual? (opcional)"
            value={form.condicoesSaude}
            onChangeText={(t) => mudar('condicoesSaude', t)}
            ajuda="Só o necessário para priorizar a visita."
          />
        ) : null}

        <View style={estilos.consentimento}>
          <Text style={estilos.consentimentoTitulo} accessibilityRole="header">
            Consentimento
          </Text>
          <Text style={estilos.consentimentoTexto}>
            Vamos medir a temperatura e a umidade da casa para avisar o agente de saúde quando estiver perigosamente quente
            ou fria. Não gravamos som nem imagem. Os dados da pessoa são usados só para isso. A decisão é da pessoa.
          </Text>
          <CaixaDeMarcar
            texto="Li para a pessoa e ela concordou"
            marcado={form.consentimento}
            aoMudar={(v) => mudar('consentimento', v)}
            erro={erros.consentimento}
          />
        </View>

        {erroGeral ? <Aviso tom="erro" texto={erroGeral} /> : null}
        <Botao titulo="Cadastrar" onPress={salvar} carregando={enviando} />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const estilos = StyleSheet.create({
  tela: { flex: 1, backgroundColor: cores.fundo },
  conteudo: { padding: 16, gap: 16, paddingBottom: 40 },
  pergunta: { fontSize: fonte.corpo, fontWeight: '700', color: cores.texto },
  consentimento: { gap: 10, backgroundColor: cores.cartao, borderRadius: 16, padding: 16, borderWidth: 1, borderColor: cores.borda },
  consentimentoTitulo: { fontSize: fonte.corpo + 1, fontWeight: '800', color: cores.texto },
  consentimentoTexto: { fontSize: fonte.corpo, color: cores.textoSuave },
});
