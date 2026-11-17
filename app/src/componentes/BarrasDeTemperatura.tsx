import { StyleSheet, Text, View } from 'react-native';

import type { LeituraResumo } from '@/api/tipos';
import { cores, fonte } from '@/tema/tema';
import { aparenciaDe } from '@/util/semaforo';

/** Mini-gráfico: uma barra por leitura (da mais antiga à mais nova), na cor do semáforo de cada uma. */
export function BarrasDeTemperatura({ leituras }: { leituras: LeituraResumo[] }) {
  if (leituras.length < 2) return null;
  const temperaturas = leituras.map((l) => l.temperaturaC);
  const minimo = Math.min(...temperaturas) - 2;
  const maximo = Math.max(...temperaturas) + 2;
  const faixa = maximo - minimo || 1;
  const primeira = temperaturas[0];
  const ultima = temperaturas[temperaturas.length - 1];

  return (
    <View
      accessible
      accessibilityLabel={`Gráfico das últimas ${leituras.length} leituras. Foi de ${primeira.toFixed(1)} para ${ultima.toFixed(1)} graus.`}
    >
      <View style={estilos.barras}>
        {leituras.map((l, i) => (
          <View
            key={`${l.recebidaEm}-${i}`}
            style={[estilos.barra, { height: 12 + ((l.temperaturaC - minimo) / faixa) * 78, backgroundColor: aparenciaDe(l.nivel).fundo }]}
          />
        ))}
      </View>
      <View style={estilos.legenda}>
        <Text style={estilos.texto}>mais antiga</Text>
        <Text style={estilos.texto}>mais nova</Text>
      </View>
    </View>
  );
}

const estilos = StyleSheet.create({
  barras: { flexDirection: 'row', alignItems: 'flex-end', gap: 4, height: 92 },
  barra: { flex: 1, borderRadius: 4, minWidth: 6 },
  legenda: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 4 },
  texto: { fontSize: fonte.pequena - 2, color: cores.textoSuave },
});
