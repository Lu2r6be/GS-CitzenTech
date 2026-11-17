#!/usr/bin/env bash
# falha-controlada.sh - falha DE PROPOSITO para provar que o erro de um script e detectado e registrado.
#
# Uso:   ./falha-controlada.sh
# Resultado esperado: o script termina com codigo diferente de 0 e o log traz, em ordem:
#   ERRO comando falhou ... / ERRO falhou na linha N ... / ERRO fim COM ERRO, codigo 2
set -Eeuo pipefail

DIR_DESTE_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../scripts/lib/comum.sh
source "${DIR_DESTE_SCRIPT}/../scripts/lib/comum.sh"
iniciar_script "teste-falha-controlada"

log INFO "TESTE: vou executar um comando que NAO pode dar certo"
executar "listando uma pasta que nao existe" ls /pasta/que/nao/existe
log INFO "esta linha nunca deve aparecer no log (o script para no erro)"
