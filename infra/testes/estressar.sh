#!/usr/bin/env bash
# estressar.sh - gera carga de proposito para o monitor soltar ALERTA (teste de falha do roteiro).
#
# Uso:   sudo ./estressar.sh cpu [segundos]        enche todos os nucleos (padrao 90 s)
#        sudo ./estressar.sh memoria [segundos]    ocupa ~90% da RAM livre (padrao 90 s)
#        sudo ./estressar.sh disco [megabytes]     cria um arquivo grande e apaga ao final (padrao 2048 MB)
#
# Dica: enquanto roda, em outro terminal:   tail -f /var/log/termometro/monitor.log
# O monitor mede a cada 30 s; por isso o teste dura mais que isso.
set -Eeuo pipefail

DIR_DESTE_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../scripts/lib/comum.sh
source "${DIR_DESTE_SCRIPT}/../scripts/lib/comum.sh"
iniciar_script "teste-estresse"
exigir_root

tipo="${1:-}"
valor="${2:-}"

case "$tipo" in
  cpu)
    segundos="${valor:-90}"
    log INFO "TESTE: carga de CPU em todos os nucleos por ${segundos}s"
    executar "stress-ng (cpu)" stress-ng --cpu 0 --timeout "${segundos}s" --metrics-brief
    ;;
  memoria)
    segundos="${valor:-90}"
    log INFO "TESTE: ocupando ~90% da memoria por ${segundos}s"
    executar "stress-ng (memoria)" stress-ng --vm 1 --vm-bytes 90% --vm-keep --timeout "${segundos}s" --metrics-brief
    ;;
  disco)
    mb="${valor:-2048}"
    arquivo="/var/tmp/termometro-teste-disco.bin"
    log INFO "TESTE: criando ${mb} MB em ${arquivo} (apagado ao final)"
    # shellcheck disable=SC2064
    trap "rm -f '$arquivo'; log INFO 'arquivo de teste apagado'" EXIT
    executar "criando arquivo grande" fallocate -l "${mb}M" "$arquivo"
    log INFO "uso do disco agora: $(df --output=pcent / | tail -n1 | tr -d ' ')"
    log INFO "aguardando 70 s para o monitor medir (Ctrl+C apaga o arquivo e encerra)"
    sleep 70
    ;;
  *)
    echo "Uso: sudo $0 cpu|memoria|disco [valor]" >&2
    exit 1
    ;;
esac
log INFO "TESTE concluido: confira os ALERTAs em /var/log/termometro/monitor.log"
