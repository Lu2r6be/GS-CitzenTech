import { Pressable, StyleSheet, Text, View } from 'react-native';

import type { CasaNaFila } from '@/api/tipos';
import { cores, fonte } from '@/tema/tema';
import { haQuantoTempo } from '@/util/datas';
import { aparenciaDe } from '@/util/semaforo';

import { SemaforoSelo } from './SemaforoSelo';

interface Props {
  casa: CasaNaFila;
  posicao: number;
  /** Há visita feita neste celular que ainda não foi enviada. */
  comVisitaPendente: boolean;
  onPress: () => void;
}

export function formatarTemperatura(valor: number | null): string {
  return valor === null ? 'sem leitura' : `${valor.toFixed(1).replace('.', ',')} °C`;
}

export function CartaoDaCasa({ casa, posicao, comVisitaPendente, onPress }: Props) {
  const a = aparenciaDe(casa.nivel);
  const visita = casa.ultimaVisitaEm ? `Última visita ${haQuantoTempo(casa.ultimaVisitaEm)}` : 'Nunca visitada';
  const leitura = casa.semSinal ? 'Sem sinal do sensor' : `${formatarTemperatura(casa.temperaturaC)} · ${haQuantoTempo(casa.ultimaLeituraEm)}`;

  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`${posicao}º da fila. ${casa.nomeResidente}, ${casa.endereco}. ${a.rotulo}: ${a.explicacao}. ${leitura}. ${visita}.${
        comVisitaPendente ? ' Visita guardada no celular, ainda não enviada.' : ''
      }`}
      accessibilityHint="Abre os detalhes da casa"
      style={({ pressed }) => [estilos.cartao, pressed && { opacity: 0.85 }]}
    >
      <SemaforoSelo nivel={casa.nivel} />
      <View style={estilos.textos}>
        <Text style={estilos.nome}>
          {posicao}. {casa.nomeResidente}
        </Text>
        <Text style={estilos.linha}>{casa.endereco}</Text>
        <Text style={estilos.linha}>{leitura}</Text>
        <Text style={estilos.linha}>
          {visita} · Vulnerabilidade {casa.pontosDeVulnerabilidade}
        </Text>
        {comVisitaPendente ? <Text style={estilos.pendente}>Visita guardada no celular, falta enviar</Text> : null}
      </View>
    </Pressable>
  );
}

const estilos = StyleSheet.create({
  cartao: {
    flexDirection: 'row',
    gap: 12,
    alignItems: 'center',
    backgroundColor: cores.cartao,
    borderRadius: 16,
    padding: 14,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: cores.borda,
  },
  textos: { flex: 1, gap: 2 },
  nome: { fontSize: fonte.corpo + 1, fontWeight: '800', color: cores.texto },
  linha: { fontSize: fonte.pequena, color: cores.textoSuave },
  pendente: { fontSize: fonte.pequena, fontWeight: '700', color: cores.avisoTexto },
});
