#!/usr/bin/env bash
# provision-app.sh - instala e liga a APLICACAO na maquina ja preparada pelo provision-infra.sh:
# .NET, banco PostgreSQL, publicacao da API, variaveis de ambiente, servicos systemd e tunel.
#
# Uso:   sudo ./provision-app.sh
#        DRY_RUN=1 ./provision-app.sh        (so mostra o que faria)
#
# De onde vem o codigo (nesta ordem): SRC_DIR (pasta) > REPO_URL (git clone) > o proprio repositorio
# onde este script esta (infra/scripts/../../api).
# Idempotente: rodar de novo atualiza a publicacao e reaproveita os segredos que ja existem.
set -Eeuo pipefail

DIR_DESTE_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DIR_PROJETO="$(cd "${DIR_DESTE_SCRIPT}/.." && pwd)"
# shellcheck source=lib/comum.sh
source "${DIR_DESTE_SCRIPT}/lib/comum.sh"

iniciar_script "provision-app"
exigir_root

PORTA_API=5080
REPO_URL=""
REPO_BRANCH="main"
SRC_DIR=""
HABILITAR_DEMO=1
DEMO_SENHA="Demo@2026!"                       # senha das contas FICTICIAS de demonstracao
DEMO_CHAVE_DISPOSITIVO="chave-demo-do-esp32-001"   # a mesma que o firmware do Wokwi usa
carregar_config "${DIR_PROJETO}/config/provisionamento.conf"

GRUPO_APP="termometro"
DIR_APP="/opt/termometro/app"
DIR_FONTE="/opt/termometro/fonte"
ARQ_ENV="/etc/termometro/termometro.env"
DIR_SYSTEMD_ORIGEM="${DIR_PROJETO}/systemd"
BANCO_NOME="termometro"
BANCO_USUARIO="termometro_app"
DOTNET_DIR="/usr/share/dotnet"

# ---------- 1. Dependencias ----------
passo_dotnet() {
  log INFO "== 1/7 .NET SDK 10 =="
  if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
    log INFO ".NET 10 ja instalado: $(dotnet --version)"
    return 0
  fi
  if [[ "$DRY_RUN" != "1" ]] && apt-cache policy dotnet-sdk-10.0 2>/dev/null | grep -q 'Candidate: [0-9]'; then
    garantir_pacotes dotnet-sdk-10.0
  else
    log INFO "pacote dotnet-sdk-10.0 nao esta nos repositorios; uso o instalador oficial da Microsoft"
    executar "baixando o instalador oficial" curl -fsSL -o /var/tmp/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
    executar "instalando o SDK 10.0 em ${DOTNET_DIR}" env TMPDIR=/var/tmp bash /var/tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_DIR"
    executar "criando o atalho /usr/local/bin/dotnet" ln -sf "${DOTNET_DIR}/dotnet" /usr/local/bin/dotnet
  fi
  [[ "$DRY_RUN" == "1" ]] || log OK ".NET instalado: $(dotnet --version)"
}

caminho_dotnet() {
  if [[ "$DRY_RUN" == "1" && -z "$(command -v dotnet || true)" ]]; then echo "/usr/bin/dotnet"; return; fi
  readlink -f "$(command -v dotnet)"
}

# ---------- 2. Codigo-fonte ----------
passo_fonte() {
  log INFO "== 2/7 Codigo-fonte =="
  local raiz_local; raiz_local="$(cd "${DIR_PROJETO}/.." && pwd)"
  if [[ -n "$SRC_DIR" ]]; then
    FONTE="$SRC_DIR"; log INFO "usando a pasta indicada em SRC_DIR: ${FONTE}"
  elif [[ -n "$REPO_URL" ]]; then
    FONTE="$DIR_FONTE"
    if [[ -d "${FONTE}/.git" ]]; then
      executar "atualizando o clone em ${FONTE}" git -C "$FONTE" pull --ff-only
    else
      executar "clonando ${REPO_URL}" git clone --depth 1 --branch "$REPO_BRANCH" "$REPO_URL" "$FONTE"
    fi
  elif [[ -d "${raiz_local}/api/src/Termometro.Api" ]]; then
    FONTE="$raiz_local"; log INFO "usando o repositorio onde este script esta: ${FONTE}"
  else
    log ERRO "nao achei o codigo. Defina REPO_URL ou SRC_DIR em config/provisionamento.conf"; exit 1
  fi
  PROJETO_API="${FONTE}/api/src/Termometro.Api"
  if [[ "$DRY_RUN" != "1" && ! -d "$PROJETO_API" ]]; then
    log ERRO "pasta da API nao encontrada: ${PROJETO_API}"; exit 1
  fi
}

# ---------- 3. Banco de dados ----------
ler_do_env() {  # ler_do_env CHAVE -> valor (vazio se nao existir)
  [[ -f "$ARQ_ENV" ]] || return 0
  grep -E "^$1=" "$ARQ_ENV" | head -n1 | cut -d= -f2- || true
}

passo_banco() {
  log INFO "== 3/7 PostgreSQL =="
  executar "garantindo o PostgreSQL ligado e no boot" systemctl enable --now postgresql
  SENHA_BANCO="$(ler_do_env BANCO_SENHA)"
  [[ -n "$SENHA_BANCO" ]] || SENHA_BANCO="$(openssl rand -hex 24)"
  if [[ "$DRY_RUN" == "1" ]]; then
    log SIMULADO "criaria/atualizaria o papel ${BANCO_USUARIO} e o banco ${BANCO_NOME}"; return 0
  fi
  # SQL por entrada padrao: a senha nao aparece na lista de processos nem no log.
  if sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='${BANCO_USUARIO}'" | grep -q 1; then
    log INFO "papel ${BANCO_USUARIO} ja existe; atualizo a senha"
    printf "ALTER ROLE %s WITH LOGIN PASSWORD '%s';\n" "$BANCO_USUARIO" "$SENHA_BANCO" | sudo -u postgres psql -q -v ON_ERROR_STOP=1
  else
    log INFO "criando o papel ${BANCO_USUARIO}"
    printf "CREATE ROLE %s WITH LOGIN PASSWORD '%s';\n" "$BANCO_USUARIO" "$SENHA_BANCO" | sudo -u postgres psql -q -v ON_ERROR_STOP=1
  fi
  if sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='${BANCO_NOME}'" | grep -q 1; then
    log INFO "banco ${BANCO_NOME} ja existe"
  else
    executar "criando o banco ${BANCO_NOME}" sudo -u postgres createdb -O "$BANCO_USUARIO" "$BANCO_NOME"
  fi
  log OK "PostgreSQL pronto (as tabelas sao criadas pela API na primeira partida, via migrations)"
}

# ---------- 4. Publicar a API ----------
passo_publicar() {
  log INFO "== 4/7 Publicando a API (dotnet publish -c Release) =="
  local destino_tmp="${DIR_APP}.novo"
  executar "limpando pasta temporaria" rm -rf "$destino_tmp"
  # HOME=/root: o dotnet guarda cache de pacotes ali; telemetria desligada.
  executar "compilando e publicando" env HOME=/root DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 \
    dotnet publish "$PROJETO_API" -c Release -o "$destino_tmp" --nologo
  if [[ "$DRY_RUN" != "1" ]]; then
    # Troca rapida: a pasta antiga vira .anterior (volta atras se der problema).
    rm -rf "${DIR_APP}.anterior"
    [[ -d "$DIR_APP" ]] && mv "$DIR_APP" "${DIR_APP}.anterior"
    mv "$destino_tmp" "$DIR_APP"
    chown -R root:"$GRUPO_APP" "$DIR_APP"
    chmod -R u=rwX,g=rX,o= "$DIR_APP"
    log OK "API publicada em ${DIR_APP}"
  fi
}

# ---------- 5. Variaveis de ambiente (segredos) ----------
passo_ambiente() {
  log INFO "== 5/7 Variaveis de ambiente em ${ARQ_ENV} =="
  local chave_jwt; chave_jwt="$(ler_do_env Jwt__Chave)"
  [[ -n "$chave_jwt" ]] || chave_jwt="$(openssl rand -hex 32)"
  local demo="true"; [[ "$HABILITAR_DEMO" == "1" ]] || demo="false"
  escrever_arquivo "$ARQ_ENV" 640 root:"$GRUPO_APP" <<ENV
# Gerado por provision-app.sh. Contem SEGREDOS: nunca vai para o Git. Modo 640 (so root e o grupo ${GRUPO_APP}).
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:${PORTA_API}
DOTNET_CLI_TELEMETRY_OPTOUT=1
BANCO_SENHA=${SENHA_BANCO}
Banco__Provedor=Postgres
Banco__ConexaoPostgres=Host=127.0.0.1;Port=5432;Database=${BANCO_NOME};Username=${BANCO_USUARIO};Password=${SENHA_BANCO}
Banco__AplicarMigrations=true
Jwt__Chave=${chave_jwt}
Demonstracao__Habilitada=${demo}
Demonstracao__SenhaDosUsuarios=${DEMO_SENHA}
Demonstracao__ChaveDoDispositivo=${DEMO_CHAVE_DISPOSITIVO}
ENV
  escrever_arquivo /etc/termometro/monitor.conf 644 root:root < "${DIR_PROJETO}/config/monitor.conf.example"
}

# ---------- 6. Servicos systemd ----------
instalar_cloudflared() {
  if command -v cloudflared >/dev/null 2>&1; then log INFO "cloudflared ja instalado: $(cloudflared --version 2>&1 | head -n1)"; return 0; fi
  local arq; case "$(dpkg --print-architecture)" in
    amd64) arq="amd64" ;; arm64) arq="arm64" ;; *) log AVISO "arquitetura sem cloudflared automatico; pulei"; return 0 ;;
  esac
  executar "baixando o cloudflared" curl -fsSL -o /tmp/cloudflared.deb "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-${arq}.deb"
  executar "instalando o cloudflared" dpkg -i /tmp/cloudflared.deb
}

passo_servicos() {
  log INFO "== 6/7 Servicos systemd =="
  instalar_cloudflared
  local dotnet_bin; dotnet_bin="$(caminho_dotnet)"
  local u
  for u in termometro-api.service termometro-monitor.service termometro-guardiao.service termometro-guardiao.timer termometro-cloudflared.service; do
    # O unico campo variavel e o caminho do dotnet; o resto do arquivo vai como esta no repositorio.
    sed -e "s|@DOTNET@|${dotnet_bin}|g" -e "s|@PORTA_API@|${PORTA_API}|g" -e "s|@DIR_SCRIPTS@|/opt/termometro/scripts|g" \
      "${DIR_SYSTEMD_ORIGEM}/${u}" | escrever_arquivo "/etc/systemd/system/${u}" 644 root:root
  done
  # Os scripts de operacao ficam em /opt/termometro/scripts (o systemd roda de la).
  executar "criando /opt/termometro/scripts" install -d -m 0755 -o root -g root /opt/termometro/scripts/lib
  executar "copiando monitor.sh e guardiao.sh" install -m 0755 -o root -g root \
    "${DIR_DESTE_SCRIPT}/monitor.sh" "${DIR_DESTE_SCRIPT}/guardiao.sh" "${DIR_DESTE_SCRIPT}/mostrar-url-tunel.sh" /opt/termometro/scripts/
  executar "copiando lib/comum.sh" install -m 0644 -o root -g root "${DIR_DESTE_SCRIPT}/lib/comum.sh" /opt/termometro/scripts/lib/

  executar "recarregando o systemd" systemctl daemon-reload
  executar "ligando e habilitando a API no boot" systemctl enable --now termometro-api.service
  executar "ligando e habilitando o monitor no boot" systemctl enable --now termometro-monitor.service
  executar "ligando e habilitando o guardiao (timer) no boot" systemctl enable --now termometro-guardiao.timer
  executar "ligando e habilitando o tunel no boot" systemctl enable --now termometro-cloudflared.service
}

# ---------- 7. Verificacao ----------
passo_verificar() {
  log INFO "== 7/7 Verificacao =="
  if [[ "$DRY_RUN" == "1" ]]; then log SIMULADO "verificaria /health e os servicos"; return 0; fi
  local tentativa ok=0
  for tentativa in $(seq 1 30); do
    if curl -fsS -m 3 -o /dev/null "http://127.0.0.1:${PORTA_API}/health"; then ok=1; break; fi
    sleep 2
  done
  if [[ "$ok" == "1" ]]; then log OK "API respondeu em /health (tentativa ${tentativa})"
  else log ERRO "API nao respondeu em /health. Veja: journalctl -u termometro-api -n 80"; exit 1; fi
  local s
  for s in postgresql termometro-api termometro-monitor termometro-guardiao.timer termometro-cloudflared; do
    if systemctl is-active --quiet "$s"; then log OK "servico ativo: ${s}"; else log ERRO "servico NAO ativo: ${s}"; fi
  done
  for s in postgresql termometro-api termometro-monitor termometro-guardiao.timer termometro-cloudflared; do
    log INFO "habilitado no boot: ${s} = $(systemctl is-enabled "$s" 2>&1)"
  done
  log INFO "URL publica do tunel (pode demorar alguns segundos): rode ./mostrar-url-tunel.sh"
}

passo_dotnet
passo_fonte
passo_banco
passo_publicar
passo_ambiente
passo_servicos
passo_verificar
log OK "aplicacao pronta."
