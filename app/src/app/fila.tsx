import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, FlatList, StyleSheet, Text, View } from 'react-native';

import { ErroDeRede, SessaoExpirada } from '@/api/cliente';
import type { FilaDeVisitas } from '@/api/tipos';
import type { VisitaLocal } from '@/banco/repositorios';
import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { CartaoDaCasa } from '@/componentes/CartaoDaCasa';
import { Carregando } from '@/componentes/Carregando';
import { EstadoDeErro } from '@/componentes/EstadoDeErro';
import { useApp } from '@/contexto/AppContexto';
import { cores, fonte } from '@/tema/tema';
import { formatarHora } from '@/util/datas';
import { avisoDoDia, tituloDoResultado } from '@/util/semaforo';

const A_CADA_MS = 30_000;

export default function Fila() {
  const { api, cache, visitas, sincronizar } = useApp();
  const router = useRouter();

  const [fila, setFila] = useState<FilaDeVisitas | null>(null);
  const [vindaDoCache, setVindaDoCache] = useState(false);
  const [atualizadaEm, setAtualizadaEm] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [primeiraCarga, setPrimeiraCarga] = useState(true);
  const [atualizando, setAtualizando] = useState(false);
  const [pendentes, setPendentes] = useState(0);
  const [casasPendentes, setCasasPendentes] = useState<Set<string>>(new Set());
  const [recusadas, setRecusadas] = useState<VisitaLocal[]>([]);
  const [resultadoDoEnvio, setResultadoDoEnvio] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const lerCaixaDeSaida = useCallback(async () => {
    setPendentes(await visitas.contarPendentes());
    setCasasPendentes(await visitas.casasComPendencia());
    setRecusadas(await visitas.rejeitadas());
  }, [visitas]);

  const carregar = useCallback(async () => {
    setErro(null);
    // 1) manda o que ficou guardado no celular; 2) busca a fila nova.
    try {
      const resumo = await sincronizar();
      if (resumo.enviadas > 0) {
        setResultadoDoEnvio(`${resumo.enviadas} visita(s) enviada(s) ao servidor.`);
      }
    } catch (e) {
      if (e instanceof SessaoExpirada) return;
      // outros erros de envio não impedem de ver a fila; as visitas seguem guardadas
    }

    try {
      const nova = await api.fila();
      await cache.salvar('fila', nova);
      setFila(nova);
      setVindaDoCache(false);
      setAtualizadaEm(new Date().toISOString());
    } catch (e) {
      if (e instanceof SessaoExpirada) return;
      const guardada = await cache.ler<FilaDeVisitas>('fila');
      if (guardada) {
        setFila(guardada.valor);
        setVindaDoCache(true);
        setAtualizadaEm(guardada.salvoEm);
        if (!(e instanceof ErroDeRede)) setErro(e instanceof Error ? e.message : 'Erro desconhecido.');
      } else {
        setErro(
          e instanceof ErroDeRede
            ? 'Sem internet e ainda não há uma fila guardada neste celular. Conecte-se uma vez para baixar.'
            : e instanceof Error
              ? e.message
              : 'Erro desconhecido.',
        );
      }
    } finally {
      setPrimeiraCarga(false);
      await lerCaixaDeSaida();
    }
  }, [api, cache, sincronizar, lerCaixaDeSaida]);

  // Atualiza ao abrir a tela, a cada 30 s enquanto ela está aberta e quando o app volta ao primeiro plano.
  const carregarRef = useRef(carregar);
  carregarRef.current = carregar;
  useFocusEffect(
    useCallback(() => {
      void carregarRef.current();
      const relogio = setInterval(() => void carregarRef.current(), A_CADA_MS);
      return () => clearInterval(relogio);
    }, []),
  );
  useEffect(() => {
    const assinatura = AppState.addEventListener('change', (estado) => {
      if (estado === 'active') void carregarRef.current();
    });
    return () => assinatura.remove();
  }, []);

  async function puxarParaAtualizar() {
    setAtualizando(true);
    await carregar();
    setAtualizando(false);
  }

  async function enviarAgora() {
    setEnviando(true);
    await carregar();
    setEnviando(false);
  }

  if (primeiraCarga) return <Carregando texto="Buscando a fila de visitas..." />;
  if (!fila) return <EstadoDeErro mensagem={erro ?? 'Não foi possível carregar a fila.'} aoTentarDeNovo={() => void carregar()} />;

  const aviso = avisoDoDia(fila.diaDeRisco);

  return (
    <FlatList
      style={estilos.lista}
      contentContainerStyle={estilos.conteudo}
      data={fila.casas}
      keyExtractor={(casa) => casa.domicilioId}
      refreshing={atualizando}
      onRefresh={puxarParaAtualizar}
      renderItem={({ item, index }) => (
        <CartaoDaCasa
          casa={item}
          posicao={index + 1}
          comVisitaPendente={casasPendentes.has(item.domicilioId)}
          onPress={() => router.push({ pathname: '/casa/[id]', params: { id: item.domicilioId } })}
        />
      )}
      ListHeaderComponent={
        <View style={estilos.cabecalho}>
          <Text style={estilos.resumo} accessibilityRole="header">
            {fila.casas.length} {fila.casas.length === 1 ? 'casa' : 'casas'} · a mais urgente primeiro
          </Text>
          {aviso ? <Aviso tom="atencao" texto={aviso} /> : null}
          {vindaDoCache && atualizadaEm ? (
            <Aviso
              tom="atencao"
              texto={`Sem internet. Você está vendo a fila guardada às ${formatarHora(atualizadaEm)}; as cores podem ter mudado.`}
            />
          ) : null}
          {erro ? <Aviso tom="erro" texto={erro} /> : null}
          {pendentes > 0 ? (
            <Aviso tom="info" texto={`${pendentes} visita(s) guardada(s) neste celular, esperando internet para serem enviadas.`}>
              <Botao titulo="Enviar agora" variante="secundario" onPress={enviarAgora} carregando={enviando} />
            </Aviso>
          ) : null}
          {resultadoDoEnvio ? <Aviso tom="info" texto={resultadoDoEnvio} /> : null}
          {recusadas.map((visita) => (
            <Aviso
              key={visita.id}
              tom="erro"
              texto={`O servidor recusou uma visita (${tituloDoResultado(visita.resultado)}): ${visita.motivoRejeicao ?? 'sem motivo informado'}`}
            >
              <Botao
                titulo="Entendi, descartar"
                variante="secundario"
                onPress={async () => {
                  await visitas.descartarRejeitada(visita.id);
                  await lerCaixaDeSaida();
                }}
              />
            </Aviso>
          ))}
        </View>
      }
      ListEmptyComponent={
        <Text style={estilos.vazio}>Nenhuma casa na sua microárea ainda. Cadastre o primeiro residente abaixo.</Text>
      }
      ListFooterComponent={
        <View style={estilos.rodape}>
          <Botao titulo="Cadastrar residente" variante="secundario" onPress={() => router.push('/cadastro')} />
          {atualizadaEm ? (
            <Text style={estilos.pequeno}>
              Atualizada às {formatarHora(atualizadaEm)}. Puxe a lista para baixo para atualizar.
            </Text>
          ) : null}
        </View>
      }
    />
  );
}

const estilos = StyleSheet.create({
  lista: { flex: 1, backgroundColor: cores.fundo },
  conteudo: { padding: 16 },
  cabecalho: { gap: 10, marginBottom: 12 },
  resumo: { fontSize: fonte.titulo, fontWeight: '800', color: cores.texto },
  vazio: { fontSize: fonte.corpo, color: cores.textoSuave, textAlign: 'center', marginVertical: 24 },
  rodape: { gap: 12, marginTop: 8, marginBottom: 24 },
  pequeno: { fontSize: fonte.pequena, color: cores.textoSuave, textAlign: 'center' },
});
