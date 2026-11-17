#!/usr/bin/env bash
# monitor.sh - mede CPU, RAM e disco, confere os servicos e a API e registra tudo em log.
#
# Uso:
#   monitor.sh              roda em ciclos, de tempos em tempos (e assim que o servico systemd o usa)
#   monitor.sh --uma-vez    mede uma vez e sai com um codigo (bom para testar):
#                             0 = tudo certo | 1 = algum limite estourou (ALERTA) | 2 = algo parado ou sem resposta (FALHA)
#
# O monitor so OBSERVA e avisa. Quem religa o que parou e o guardiao.sh.
set -uo pipefail

DIR_DESTE_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/comum.sh
source "${DIR_DESTE_SCRIPT}/lib/comum.sh"

ARQUIVO_CONFIG="${ARQUIVO_CONFIG:-/etc/termometro/monitor.conf}"
CMD_SYSTEMCTL="${CMD_SYSTEMCTL:-systemctl}"

# Um erro isolado (ex.: curl falhou) nao pode derrubar o monitor: cada falha e tratada de forma explicita.
# Por isso desligamos o "parar no primeiro erro" que o iniciar_script liga.
iniciar_script "monitor"
set +e
trap - ERR

ler_config() {
  # Valores padrao; o arquivo de configuracao (se existir) sobrescreve. Relido a cada ciclo.
  INTERVALO_SEGUNDOS=30
  CPU_LIMITE=85
  RAM_LIMITE=85
  DISCO_LIMITE=85
  DISCO_CAMINHO="/"
  URL_SAUDE="http://127.0.0.1:5080/health"
  SERVICOS="postgresql termometro-api termometro-cloudflared"
  if [[ -f "$ARQUIVO_CONFIG" ]]; then
    # shellcheck disable=SC1090
    source "$ARQUIVO_CONFIG"
  fi
}

# Uso de CPU em %: compara duas leituras de /proc/stat com 1 segundo de intervalo.
cpu_percentual() {
  local u1 n1 s1 i1 w1 q1 sq1 st1 u2 n2 s2 i2 w2 q2 sq2 st2
  read -r _ u1 n1 s1 i1 w1 q1 sq1 st1 _ < /proc/stat
  sleep 1
  read -r _ u2 n2 s2 i2 w2 q2 sq2 st2 _ < /proc/stat
  local total1=$((u1 + n1 + s1 + i1 + w1 + q1 + sq1 + st1))
  local total2=$((u2 + n2 + s2 + i2 + w2 + q2 + sq2 + st2))
  local total=$((total2 - total1))
  local ocioso=$(((i2 + w2) - (i1 + w1)))
  if ((total <= 0)); then echo 0; else echo $((100 * (total - ocioso) / total)); fi
}

# Uso de memoria em %: usa MemAvailable (o que de fato da para usar), nao so a memoria "livre".
ram_percentual() {
  awk '/^MemTotal:/ {t=$2} /^MemAvailable:/ {a=$2} END { if (t > 0) printf "%d", (t - a) * 100 / t; else print 0 }' /proc/meminfo
}

disco_percentual() {
  df --output=pcent "$DISCO_CAMINHO" 2>/dev/null | tail -n 1 | tr -dc '0-9'
}

servicos_parados() {
  local s parados=()
  for s in $SERVICOS; do
    "$CMD_SYSTEMCTL" is-active --quiet "$s" || parados+=("$s")
  done
  echo "${parados[*]:-}"
}

api_responde() {
  command -v curl >/dev/null 2>&1 || return 0   # sem curl nao da para testar: nao acusa falha falsa
  curl -fsS -m 5 -o /dev/null "$URL_SAUDE" 2>/dev/null
}

# Faz uma medicao completa e registra. Devolve 0 (ok), 1 (alerta) ou 2 (falha).
um_ciclo() {
  ler_config
  local cpu ram disco parados saude="ok" item
  local alertas=() falhas=()

  cpu="$(cpu_percentual)"
  ram="$(ram_percentual)"
  disco="$(disco_percentual)"; disco="${disco:-0}"
  parados="$(servicos_parados)"

  ((cpu > CPU_LIMITE)) && alertas+=("CPU em ${cpu}% (limite ${CPU_LIMITE}%)")
  ((ram > RAM_LIMITE)) && alertas+=("RAM em ${ram}% (limite ${RAM_LIMITE}%)")
  ((disco > DISCO_LIMITE)) && alertas+=("DISCO em ${disco}% em ${DISCO_CAMINHO} (limite ${DISCO_LIMITE}%)")
  [[ -n "$parados" ]] && falhas+=("servicos parados: ${parados}")
  if ! api_responde; then
    saude="falhou"
    falhas+=("a API nao respondeu em ${URL_SAUDE}")
  fi

  log INFO "cpu=${cpu}% ram=${ram}% disco=${disco}% api=${saude} servicos_parados=${parados:-nenhum}"
  for item in ${alertas[@]+"${alertas[@]}"}; do log ALERTA "$item"; done
  for item in ${falhas[@]+"${falhas[@]}"}; do log ERRO "FALHA: ${item}"; done

  if ((${#falhas[@]} > 0)); then return 2; fi
  if ((${#alertas[@]} > 0)); then return 1; fi
  return 0
}

# Parada normal (systemctl stop): registra e sai com sucesso. kill -9 nao da tempo de registrar;
# nesse caso o systemd anota a morte e o Restart=always religa o monitor.
trap 'log INFO "recebi pedido de parada; encerrando"; exit 0' TERM INT

if [[ "${1:-}" == "--uma-vez" ]]; then
  um_ciclo
  exit $?
fi

ler_config
log INFO "monitor em ciclos: a cada ${INTERVALO_SEGUNDOS}s (limites cpu=${CPU_LIMITE}% ram=${RAM_LIMITE}% disco=${DISCO_LIMITE}%)"
while true; do
  um_ciclo
  # "sleep em segundo plano + wait" deixa o sinal de parada interromper a espera na hora.
  sleep "$INTERVALO_SEGUNDOS" &
  wait $!
done
