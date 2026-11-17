#!/usr/bin/env bash
# coletar-evidencias.sh - junta, em uma pasta so, os logs e o estado dos servicos para entregar no GitHub.
#
# Uso:   sudo ./coletar-evidencias.sh [pasta-destino]      (padrao: ./evidencias-coletadas-<data>)
# Depois copie a pasta para dentro de infra/logs/ no seu repositorio (scp, pendrive, pasta compartilhada).
set -Eeuo pipefail

DIR_DESTE_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/comum.sh
source "${DIR_DESTE_SCRIPT}/lib/comum.sh"
iniciar_script "coletar-evidencias"
exigir_root

destino="${1:-./evidencias-coletadas-$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$destino"
log INFO "coletando em ${destino}"

cp -a /var/log/termometro/*.log "$destino"/ 2>/dev/null || log AVISO "sem logs em /var/log/termometro"
for u in termometro-api termometro-monitor termometro-guardiao termometro-cloudflared postgresql; do
  journalctl -u "$u" --no-pager -n 500 > "${destino}/journal-${u}.txt" 2>&1 || true
  systemctl status "$u" --no-pager > "${destino}/status-${u}.txt" 2>&1 || true
done
{
  echo "# $(date --iso-8601=seconds) - $(hostname)"
  echo "## uptime";      uptime
  echo "## ultimo boot"; who -b
  echo "## memoria";     free -h
  echo "## disco";       df -h /
  echo "## ip";          ip -br addr
  echo "## unidades do projeto"; systemctl list-units 'termometro-*' --all --no-pager
  echo "## timers";      systemctl list-timers 'termometro-*' --no-pager
  echo "## habilitadas no boot"; systemctl is-enabled postgresql termometro-api termometro-monitor termometro-guardiao.timer termometro-cloudflared || true
  echo "## usuarios do projeto"; id termometro; id monitor; id operador
  echo "## permissoes"; ls -ld /opt/termometro /etc/termometro /var/lib/termometro /var/log/termometro; ls -l /etc/termometro
} > "${destino}/estado-do-sistema.txt" 2>&1 || true
# O arquivo de segredos NUNCA vai junto: so as permissoes aparecem acima.
log OK "pronto: $(find "$destino" -type f | wc -l) arquivos em ${destino}"
