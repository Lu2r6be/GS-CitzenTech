#!/usr/bin/env bash
# Funcoes comuns a todos os scripts: log, auditoria, execucao de comandos e tratamento de erro.
#
# Como usar (no inicio de cada script):
#   source "$(dirname "${BASH_SOURCE[0]}")/lib/comum.sh"
#   iniciar_script "nome-do-script"
#
# Ideia: todo script conta o que fez em um arquivo de log (auditoria). Quem le o log depois consegue
# responder "o que foi instalado, configurado e verificado, quando, e deu certo?".

[[ -n "${_COMUM_CARREGADO:-}" ]] && return 0
_COMUM_CARREGADO=1

DIR_LOG="${DIR_LOG:-/var/log/termometro}"
DRY_RUN="${DRY_RUN:-0}"            # 1 = so mostra o que faria (usado para testar sem mexer na maquina)
USAR_JOURNAL="${USAR_JOURNAL:-1}"  # 1 = copia o log tambem para o journald (journalctl)
NOME_SCRIPT="${NOME_SCRIPT:-script}"
LOG_ARQUIVO=""

agora() { date --iso-8601=seconds; }

# log NIVEL "mensagem"   (NIVEL: INFO, OK, AVISO, ERRO, ALERTA, CMD, SIMULADO, RECUPERACAO)
log() {
  local nivel="$1"; shift
  local linha
  linha="$(agora) [${NOME_SCRIPT}] ${nivel} $*"
  printf '%s\n' "$linha"
  if [[ -n "$LOG_ARQUIVO" ]]; then printf '%s\n' "$linha" >> "$LOG_ARQUIVO" 2>/dev/null || true; fi
  if [[ "$USAR_JOURNAL" == "1" ]] && command -v logger >/dev/null 2>&1; then
    local prioridade="user.info"
    case "$nivel" in ERRO) prioridade="user.err" ;; ALERTA|AVISO) prioridade="user.warning" ;; esac
    logger -t "termometro-${NOME_SCRIPT}" -p "$prioridade" -- "${nivel} $*" 2>/dev/null || true
  fi
}

# Prepara o log, liga o modo "parar no primeiro erro" e registra erros e o fim da execucao.
iniciar_script() {
  NOME_SCRIPT="$1"
  mkdir -p "$DIR_LOG" 2>/dev/null || true
  if [[ -w "$DIR_LOG" ]]; then
    LOG_ARQUIVO="${DIR_LOG}/${NOME_SCRIPT}.log"
  else
    # Sem permissao (ex.: teste sem root): usa uma pasta temporaria em vez de falhar.
    DIR_LOG="${TMPDIR:-/tmp}/termometro-logs"
    mkdir -p "$DIR_LOG"
    LOG_ARQUIVO="${DIR_LOG}/${NOME_SCRIPT}.log"
  fi
  # Log legivel e gravavel pelo grupo: o monitor roda com outro usuario e continua escrevendo no mesmo arquivo.
  touch "$LOG_ARQUIVO" 2>/dev/null && chmod 664 "$LOG_ARQUIVO" 2>/dev/null || true
  _INICIO_SEGUNDOS=$SECONDS
  set -Eeuo pipefail
  trap '_ao_falhar $? $LINENO "$BASH_COMMAND"' ERR
  trap '_ao_sair $?' EXIT
  # LOG_INICIO=0 e usado por quem roda a cada 30 s (guardiao), para o log nao encher de "inicio".
  if [[ "${LOG_INICIO:-1}" == "1" ]]; then
    log INFO "inicio (usuario=$(id -un), maquina=$(hostname), DRY_RUN=${DRY_RUN})"
  fi
}

_ao_falhar() {
  local codigo="$1" linha="$2" comando="$3"
  log ERRO "falhou na linha ${linha} (codigo ${codigo}): ${comando}"
}

_ao_sair() {
  local codigo="$1"
  local duracao=$((SECONDS - _INICIO_SEGUNDOS))
  if [[ "$codigo" -eq 0 ]]; then
    log OK "fim sem erros (${duracao}s)"
  else
    log ERRO "fim COM ERRO, codigo ${codigo} (${duracao}s)"
  fi
}

exigir_root() {
  if [[ "$(id -u)" -ne 0 && "$DRY_RUN" != "1" ]]; then
    log ERRO "este script precisa de root. Rode com: sudo $0"
    exit 1
  fi
}

# executar "o que isto faz" comando args...
# Registra a descricao e o comando no log; a saida do comando vai so para o log (e para a tela se falhar).
executar() {
  local descricao="$1"; shift
  log INFO "$descricao"
  log CMD "$*"
  if [[ "$DRY_RUN" == "1" ]]; then
    log SIMULADO "nao executado (DRY_RUN=1)"
    return 0
  fi
  local saida codigo=0
  saida="$("$@" 2>&1)" || codigo=$?
  if [[ -n "$saida" && -n "$LOG_ARQUIVO" ]]; then
    printf '%s\n' "$saida" | sed 's/^/    | /' >> "$LOG_ARQUIVO"
  fi
  if [[ "$codigo" -ne 0 ]]; then
    printf '%s\n' "$saida" | sed 's/^/    | /' >&2
    log ERRO "comando falhou (codigo ${codigo}): $*"
    return "$codigo"
  fi
}

# escrever_arquivo CAMINHO MODO DONO:GRUPO   (o conteudo vem pela entrada padrao)
# So escreve se o conteudo mudou: rodar duas vezes nao muda nada (idempotente) e o log diz o que aconteceu.
escrever_arquivo() {
  local caminho="$1" modo="$2" dono="$3"
  local novo; novo="$(mktemp)"
  cat > "$novo"
  if [[ "$DRY_RUN" == "1" ]]; then
    log SIMULADO "escreveria ${caminho} (modo ${modo}, dono ${dono})"
    rm -f "$novo"; return 0
  fi
  mkdir -p "$(dirname "$caminho")"
  if [[ -f "$caminho" ]] && cmp -s "$novo" "$caminho"; then
    log INFO "arquivo ja estava correto: ${caminho}"
  else
    if [[ -f "$caminho" ]]; then
      cp -a "$caminho" "${caminho}.bak-$(date +%Y%m%d%H%M%S)"
      log INFO "arquivo atualizado (copia .bak criada): ${caminho}"
    else
      log INFO "arquivo criado: ${caminho}"
    fi
    install -m "$modo" -o "${dono%%:*}" -g "${dono##*:}" "$novo" "$caminho"
  fi
  chmod "$modo" "$caminho"; chown "$dono" "$caminho"
  rm -f "$novo"
}

pacote_instalado() { dpkg -s "$1" >/dev/null 2>&1; }

# garantir_pacotes pacote1 pacote2 ...   (instala so o que falta)
garantir_pacotes() {
  local faltando=() p
  for p in "$@"; do
    if pacote_instalado "$p"; then log INFO "pacote ja instalado: ${p}"; else faltando+=("$p"); fi
  done
  if [[ ${#faltando[@]} -gt 0 ]]; then
    DEBIAN_FRONTEND=noninteractive executar "instalando pacotes: ${faltando[*]}" \
      env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends "${faltando[@]}"
  fi
}

# Le variaveis de um arquivo de configuracao simples (CHAVE=valor), se ele existir.
carregar_config() {
  local arquivo="$1"
  if [[ -f "$arquivo" ]]; then
    log INFO "lendo configuracao: ${arquivo}"
    # shellcheck disable=SC1090
    source "$arquivo"
  else
    log INFO "sem arquivo de configuracao (${arquivo}); usando os valores padrao"
  fi
}

chave_aleatoria() {
  local bytes="${1:-32}"
  if command -v openssl >/dev/null 2>&1; then openssl rand -base64 "$bytes" | tr -d '\n'
  else head -c "$bytes" /dev/urandom | base64 | tr -d '\n'; fi
}
