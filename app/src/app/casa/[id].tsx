import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';

import { ErroDeRede, SessaoExpirada } from '@/api/cliente';
import type { DetalheDaCasa, LeituraResumo, TipoCobertura } from '@/api/tipos';
import { Aviso } from '@/componentes/Aviso';
import { BarrasDeTemperatura } from '@/componentes/BarrasDeTemperatura';
import { Botao } from '@/componentes/Botao';
import { formatarTemperatura } from '@/componentes/CartaoDaCasa';
import { Carregando } from '@/componentes/Carregando';
import { EstadoDeErro } from '@/componentes/EstadoDeErro';
import { SemaforoSelo } from '@/componentes/SemaforoSelo';
import { useApp } from '@/contexto/AppContexto';
import { cores, fonte } from '@/tema/tema';
import { formatarHora, haQuantoTempo } from '@/util/datas';
import { aparenciaDe, textoDaTendencia } from '@/util/semaforo';

const COBERTURA: Record<TipoCobertura, string> = {
  laje: 'laje',
  telha: 'telha',
  fibrocimento: 'fibrocimento (esquenta mais)',
  outro: 'outro tipo',
};

export default function DetalheDaCasaTela() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { api, cache } = useApp();
  const router = useRouter();

  const [casa, setCasa] = useState<DetalheDaCasa | null>(null);
  const [vindaDoCache, setVindaDoCache] = useState(false);
  const [salvaEm, setSalvaEm] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [atualizando, setAtualizando] = useState(false);

  const carregar = useCallback(async () => {
    try {
      const nova = await api.casa(id);
      await cache.salvar(`casa:${id}`, nova);
      setCasa(nova);
      setVindaDoCache(false);
      setErro(null);
    } catch (e) {
      if (e instanceof SessaoExpirada) return;
      const guardada = await cache.ler<DetalheDaCasa>(`casa:${id}`);
      if (guardada) {
        setCasa(guardada.valor);
        setVindaDoCache(true);
        setSalvaEm(guardada.salvoEm);
      } else {
        setErro(
          e instanceof ErroDeRede
            ? 'Sem internet e esta casa ainda não foi guardada neste celular.'
            : e instanceof Error
              ? e.message
              : 'Erro desconhecido.',
        );
      }
    } finally {
      setCarregando(false);
    }
  }, [api, cache, id]);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  if (carregando) return <Carregando texto="Abrindo a casa..." />;
  if (!casa) return <EstadoDeErro mensagem={erro ?? 'Casa não encontrada.'} aoTentarDeNovo={() => void carregar()} />;

  // Da mais nova para a mais antiga (lista) e o contrário no gráfico.
  const maisNovasPrimeiro: LeituraResumo[] = [...casa.ultimasLeituras].sort(
    (a, b) => new Date(b.recebidaEm).getTime() - new Date(a.recebidaEm).getTime(),
  );
  const maisAntigasPrimeiro = [...maisNovasPrimeiro].reverse();
  const ultima = maisNovasPrimeiro[0];

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
        <Aviso tom="atencao" texto={`Sem internet. Dados guardados às ${formatarHora(salvaEm)}; podem estar desatualizados.`} />
      ) : null}

      <Text style={estilos.nome} accessibilityRole="header">
        {casa.nomeResidente}
      </Text>
      <Text style={estilos.linha}>{casa.endereco}</Text>

      <SemaforoSelo nivel={casa.nivel} grande />
      <Text style={estilos.explicacao}>
        {casa.semSinal
          ? 'O sensor desta casa está sem sinal. Vale ligar para a pessoa ou passar lá.'
          : `Casa ${aparenciaDe(casa.nivel).explicacao}.`}
      </Text>

      <View style={estilos.cartao}>
        <Text style={estilos.destaque}>
          {ultima ? formatarTemperatura(ultima.temperaturaC) : 'Sem leituras'}
          {ultima ? `  ·  ${Math.round(ultima.umidade)}% de umidade` : ''}
        </Text>
        <Text style={estilos.linha}>
          {ultima ? `Sensação térmica (índice de calor): ${formatarTemperatura(ultima.indiceCalorC)}` : ''}
        </Text>
        <Text style={estilos.linha}>Última leitura {haQuantoTempo(casa.ultimaLeituraEm)}</Text>
        <Text style={estilos.tendencia}>{textoDaTendencia(casa.tendencia)}</Text>
      </View>

      <BarrasDeTemperatura leituras={maisAntigasPrimeiro} />

      <View style={estilos.cartao}>
        <Text style={estilos.subtitulo} accessibilityRole="header">
          Sobre a casa
        </Text>
        <Text style={estilos.linha}>Telhado: {COBERTURA[casa.tipoCobertura] ?? casa.tipoCobertura}</Text>
        <Text style={estilos.linha}>CPF: {casa.cpfMascarado}</Text>
      </View>

      <View style={estilos.cartao}>
        <Text style={estilos.subtitulo} accessibilityRole="header">
          Últimas leituras
        </Text>
        {maisNovasPrimeiro.length === 0 ? <Text style={estilos.linha}>Ainda não chegou nenhuma leitura.</Text> : null}
        {maisNovasPrimeiro.slice(0, 8).map((l, i) => (
          <View
            key={`${l.recebidaEm}-${i}`}
            style={estilos.leitura}
            accessible
            accessibilityLabel={`${formatarHora(l.recebidaEm)}: ${l.temperaturaC.toFixed(1)} graus, ${aparenciaDe(l.nivel).rotulo}`}
          >
            <View style={[estilos.bolinha, { backgroundColor: aparenciaDe(l.nivel).fundo }]} />
            <Text style={estilos.leituraHora}>{formatarHora(l.recebidaEm)}</Text>
            <Text style={estilos.leituraTexto}>
              {formatarTemperatura(l.temperaturaC)} · {Math.round(l.umidade)}% · {aparenciaDe(l.nivel).rotulo}
            </Text>
          </View>
        ))}
      </View>

      <Botao
        titulo="Registrar visita"
        dica="Abre a tela para anotar o que aconteceu na visita a esta casa"
        onPress={() => router.push({ pathname: '/visita/[id]', params: { id: casa.id, nome: casa.nomeResidente } })}
      />
    </ScrollView>
  );
}

const estilos = StyleSheet.create({
  tela: { flex: 1, backgroundColor: cores.fundo },
  conteudo: { padding: 16, gap: 14, paddingBottom: 32 },
  nome: { fontSize: fonte.grande, fontWeight: '900', color: cores.texto },
  linha: { fontSize: fonte.corpo, color: cores.textoSuave },
  explicacao: { fontSize: fonte.corpo, color: cores.texto, textAlign: 'center' },
  cartao: { backgroundColor: cores.cartao, borderRadius: 16, padding: 16, gap: 6, borderWidth: 1, borderColor: cores.borda },
  destaque: { fontSize: fonte.titulo, fontWeight: '800', color: cores.texto },
  tendencia: { fontSize: fonte.corpo, fontWeight: '700', color: cores.texto },
  subtitulo: { fontSize: fonte.corpo + 1, fontWeight: '800', color: cores.texto, marginBottom: 4 },
  leitura: { flexDirection: 'row', alignItems: 'center', gap: 10, minHeight: 36 },
  bolinha: { width: 14, height: 14, borderRadius: 7 },
  leituraHora: { fontSize: fonte.pequena, fontWeight: '700', color: cores.texto, width: 52 },
  leituraTexto: { flex: 1, fontSize: fonte.pequena, color: cores.textoSuave },
});
