#!/usr/bin/env bash
# guardiao.sh - garante que os servicos do projeto estejam de pe e liga de novo o que parou.
#
# Roda como root, por um timer do systemd (a cada 30 s). Cobre o que o "Restart=always" nao cobre:
#   - alguem rodou "systemctl stop" (ou o servico foi desligado de outro jeito) e ele continua parado;
#   - a API esta "ligada", mas o /health nao responde ha varias verificacoes seguidas.
# Cada acao vira uma linha de log (ALERTA, RECUPERACAO ou ERRO): e a evidencia de recuperacao.
#
# Manutencao planejada: crie o arquivo /etc/termometro/manutencao e o guardiao nao mexe em nada.
# Quando terminar, apague o arquivo.
set -uo pipefail

DIR_DESTE_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/comum.sh
source "${DIR_DESTE_SCRIPT}/lib/comum.sh"

UNIDADES="${UNIDADES:-postgresql.service termometro-api.service termometro-cloudflared.service termometro-monitor.service}"
UNIDADE_API="${UNIDADE_API:-termometro-api.service}"
URL_SAUDE="${URL_SAUDE:-http://127.0.0.1:5080/health}"
ARQUIVO_MANUTENCAO="${ARQUIVO_MANUTENCAO:-/etc/termometro/manutencao}"
DIR_ESTADO="${DIR_ESTADO:-/run/termometro}"
FALHAS_DE_SAUDE_PARA_REINICIAR="${FALHAS_DE_SAUDE_PARA_REINICIAR:-3}"
CMD_SYSTEMCTL="${CMD_SYSTEMCTL:-systemctl}"
ESPERA_MAXIMA_SEGUNDOS="${ESPERA_MAXIMA_SEGUNDOS:-20}"

export LOG_INICIO=0
iniciar_script "guardiao"
set +e
trap - ERR
# O guardiao roda a cada 30 s: so o fim COM erro e interessante; o "tudo certo" fica de fora para nao poluir o log.
# shellcheck disable=SC2329  # chamada pelo trap EXIT
_ao_sair_silencioso() { [[ "$1" -ne 0 ]] && log ERRO "guardiao terminou com erro, codigo $1"; }
trap '_ao_sair_silencioso $?' EXIT

mkdir -p "$DIR_ESTADO" 2>/dev/null

esperar_ativa() {
  local unidade="$1" ate=$((SECONDS + ESPERA_MAXIMA_SEGUNDOS))
  while ((SECONDS < ate)); do
    "$CMD_SYSTEMCTL" is-active --quiet "$unidade" && return 0
    sleep 1
  done
  return 1
}

religar() {
  local unidade="$1" motivo="$2" estado
  estado="$("$CMD_SYSTEMCTL" is-active "$unidade" 2>/dev/null)"
  log ALERTA "${unidade}: ${motivo} (estado: ${estado:-desconhecido})"
  "$CMD_SYSTEMCTL" reset-failed "$unidade" >/dev/null 2>&1
  if "$CMD_SYSTEMCTL" restart "$unidade" >/dev/null 2>&1 && esperar_ativa "$unidade"; then
    log RECUPERACAO "${unidade} voltou a funcionar"
    return 0
  fi
  log ERRO "${unidade}: nao consegui religar. Veja: journalctl -u ${unidade} -n 50"
  return 1
}

if [[ -f "$ARQUIVO_MANUTENCAO" ]]; then
  log AVISO "modo manutencao ativo (${ARQUIVO_MANUTENCAO} existe): nao vou religar nada"
  exit 0
fi

# 1) Unidades paradas
for unidade in $UNIDADES; do
  if ! "$CMD_SYSTEMCTL" is-active --quiet "$unidade"; then
    religar "$unidade" "esta parada"
  fi
done

# 2) API ligada, mas sem responder o /health varias vezes seguidas
contador="${DIR_ESTADO}/falhas-de-saude"
if "$CMD_SYSTEMCTL" is-active --quiet "$UNIDADE_API"; then
  if ! command -v curl >/dev/null 2>&1 || curl -fsS -m 5 -o /dev/null "$URL_SAUDE" 2>/dev/null; then
    rm -f "$contador"
  else
    falhas=$(($(cat "$contador" 2>/dev/null || echo 0) + 1))
    echo "$falhas" > "$contador"
    log AVISO "a API nao respondeu ao /health (${falhas} de ${FALHAS_DE_SAUDE_PARA_REINICIAR} seguidas)"
    if ((falhas >= FALHAS_DE_SAUDE_PARA_REINICIAR)); then
      religar "$UNIDADE_API" "sem resposta no /health ha ${falhas} verificacoes"
      rm -f "$contador"
    fi
  fi
fi
exit 0
