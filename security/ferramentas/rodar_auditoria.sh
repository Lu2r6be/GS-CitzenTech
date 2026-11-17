#!/usr/bin/env bash
# Roda a auditoria de segurança completa, do mesmo jeito na máquina e no CI.
#   1. gera as regras a partir dos campos [Pii] do código
#   2. PROVA que as regras funcionam: o protótipo vulnerável TEM de ser acusado e o grupo de controle NÃO
#   3. varre o código de verdade (api/src): qualquer achado reprova
#   4. (se o trufflehog estiver instalado) procura segredos no repositório
# Uso: bash security/ferramentas/rodar_auditoria.sh [--enviar]   (--enviar manda o resultado ao painel; veja importar_achados.py)
set -uo pipefail
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$RAIZ" || exit 2
SAIDA="${SAIDA:-security/auditoria/resultados}"
mkdir -p "$SAIDA"
SG=(semgrep --metrics=off --disable-version-check --quiet --config security/regras/pii-taint.yml --json)
falhou=0

verde()    { printf '\033[32mOK\033[0m     %s\n' "$*"; }
vermelho() { printf '\033[31mFALHOU\033[0m %s\n' "$*"; falhou=1; }
contar()   { python3 -c "import json,sys;print(len(json.load(open(sys.argv[1]))['results']))" "$1"; }

command -v semgrep >/dev/null || { echo "Instale o Semgrep: pip install semgrep"; exit 2; }

echo "== 1/4 gerando regras"
python3 security/ferramentas/gerar_regras_pii.py "$RAIZ" | head -1

echo "== 2/4 as regras enxergam o protótipo vulnerável?"
"${SG[@]}" security/auditoria/prototipo-vulneravel > "$SAIDA/semgrep-prototipo.json" 2>/dev/null
for arq in CadastroInseguro LoginInseguro ExportacaoInsegura; do
  n=$(python3 -c "import json,sys;print(sum(1 for r in json.load(open(sys.argv[1]))['results'] if r['path'].endswith(sys.argv[2]+'.cs')))" "$SAIDA/semgrep-prototipo.json" "$arq")
  if [ "$n" -ge 1 ]; then verde "$arq.cs: $n fluxo(s) acusado(s)"; else vermelho "$arq.cs: nada acusado (a regra parou de funcionar)"; fi
done
n=$(python3 -c "import json,sys;print(sum(1 for r in json.load(open(sys.argv[1]))['results'] if r['path'].endswith('ControleSeguro.cs')))" "$SAIDA/semgrep-prototipo.json")
if [ "$n" -eq 0 ]; then verde "ControleSeguro.cs: 0 achados (sem falso positivo no controle)"; else vermelho "ControleSeguro.cs: $n falso(s) positivo(s)"; fi

echo "== 3/4 varredura do código real (api/src)"
"${SG[@]}" api/src --exclude obj --exclude bin --exclude Migrations > "$SAIDA/semgrep-codigo.json" 2>/dev/null
n=$(contar "$SAIDA/semgrep-codigo.json")
if [ "$n" -eq 0 ]; then verde "0 fluxos de dado pessoal sem sanitizar"; else
  vermelho "$n fluxo(s) de dado pessoal sem sanitizar:"
  python3 -c "
import json,sys
for r in json.load(open(sys.argv[1]))['results']:
    print('   ', r['path'], r['start']['line'], r['check_id'].split('.')[-1])" "$SAIDA/semgrep-codigo.json"
fi

echo "== 4/4 segredos no repositório (trufflehog)"
if command -v trufflehog >/dev/null; then
  trufflehog filesystem api infra firmware app security docs --no-update --json --no-verification \
    --exclude-paths=<(printf 'obj/\nbin/\nnode_modules/\nauditoria/resultados/\n') 2>/dev/null > "$SAIDA/trufflehog.jsonl"
  n=$(grep -c '"DetectorName"' "$SAIDA/trufflehog.jsonl" || true)
  if [ "${n:-0}" -eq 0 ]; then verde "0 segredos encontrados"; else vermelho "$n segredo(s) encontrado(s) (veja $SAIDA/trufflehog.jsonl; o arquivo contém o valor, NÃO versionar)"; fi
else
  echo "AVISO  trufflehog não instalado; etapa pulada (https://github.com/trufflesecurity/trufflehog/releases)"
fi

if [ "${1:-}" = "--enviar" ]; then
  echo "== enviando ao painel Risco Cidadão"
  args=(--semgrep "$SAIDA/semgrep-codigo.json")
  [ -f "$SAIDA/trufflehog.jsonl" ] && args+=(--trufflehog "$SAIDA/trufflehog.jsonl")
  python3 security/ferramentas/importar_achados.py "${args[@]}" || falhou=1
fi

echo
if [ "$falhou" -eq 0 ]; then echo "AUDITORIA APROVADA"; else echo "AUDITORIA REPROVADA"; fi
exit "$falhou"
