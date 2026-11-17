import { useCallback, useEffect, useState } from 'react';
import { RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

import { ErroDeRede, SessaoExpirada } from '@/api/cliente';
import type { CasaDoFamiliar } from '@/api/tipos';
import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { formatarTemperatura } from '@/componentes/CartaoDaCasa';
import { Carregando } from '@/componentes/Carregando';
import { EstadoDeErro } from '@/componentes/EstadoDeErro';
import { SemaforoSelo } from '@/componentes/SemaforoSelo';
import { useApp } from '@/contexto/AppContexto';
import { cores, fonte } from '@/tema/tema';
import { formatarHora, haQuantoTempo } from '@/util/datas';

/** Visão do familiar: uma tela só, com a cor, uma frase clara e a temperatura. */
export default function Familiar() {
  const { api, cache } = useApp();
  const [casa, setCasa] = useState<CasaDoFamiliar | null>(null);
  const [vindaDoCache, setVindaDoCache] = useState(false);
  const [salvaEm, setSalvaEm] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [atualizando, setAtualizando] = useState(false);

  const carregar = useCallback(async () => {
    try {
      const nova = await api.casaDoFamiliar();
      await cache.salvar('familiar', nova);
      setCasa(nova);
      setVindaDoCache(false);
      setErro(null);
    } catch (e) {
      if (e instanceof SessaoExpirada) return;
      const guardada = await cache.ler<CasaDoFamiliar>('familiar');
      if (guardada) {
        setCasa(guardada.valor);
        setVindaDoCache(true);
        setSalvaEm(guardada.salvoEm);
      } else {
        setErro(e instanceof ErroDeRede ? 'Sem internet. Conecte-se para ver como está a casa.' : e instanceof Error ? e.message : 'Erro desconhecido.');
      }
    } finally {
      setCarregando(false);
    }
  }, [api, cache]);

  useEffect(() => {
    void carregar();
    const relogio = setInterval(() => void carregar(), 60_000);
    return () => clearInterval(relogio);
  }, [carregar]);

  if (carregando) return <Carregando texto="Vendo como está a casa..." />;
  if (!casa) return <EstadoDeErro mensagem={erro ?? 'Nenhuma casa ligada à sua conta.'} aoTentarDeNovo={() => void carregar()} />;

  return (
    <ScrollView
      style={estilos.tela}
      contentContainerStyle={estilos.conteudo}
      refreshControl={
        <RefreshControl
          refreshing={atualizando}
          onRefresh={async () => {
            setAtualizando(true);
            await carregar();
            setAtualizando(false);
          }}
        />
      }
    >
      {vindaDoCache && salvaEm ? (
        <Aviso tom="atencao" texto={`Sem internet. Mostrando o que foi visto às ${formatarHora(salvaEm)}.`} />
      ) : null}

      <SemaforoSelo nivel={casa.nivel} grande />
      <Text style={estilos.mensagem} accessibilityLiveRegion="polite">
        {casa.mensagem}
      </Text>

      <View style={estilos.cartao}>
        <Text style={estilos.temperatura}>{casa.semSinal ? 'Sem leitura recente' : formatarTemperatura(casa.temperaturaC)}</Text>
        <Text style={estilos.linha}>Última leitura {haQuantoTempo(casa.ultimaLeituraEm)}</Text>
      </View>

      <Botao
        titulo="Atualizar agora"
        variante="secundario"
        onPress={async () => {
          setAtualizando(true);
          await carregar();
          setAtualizando(false);
        }}
      />
    </ScrollView>
  );
}

const estilos = StyleSheet.create({
  tela: { flex: 1, backgroundColor: cores.fundo },
  conteudo: { padding: 20, gap: 18 },
  mensagem: { fontSize: fonte.titulo, fontWeight: '700', color: cores.texto, textAlign: 'center' },
  cartao: { backgroundColor: cores.cartao, borderRadius: 16, padding: 20, gap: 6, alignItems: 'center', borderWidth: 1, borderColor: cores.borda },
  temperatura: { fontSize: fonte.enorme, fontWeight: '900', color: cores.texto, textAlign: 'center' },
  linha: { fontSize: fonte.corpo, color: cores.textoSuave },
});
