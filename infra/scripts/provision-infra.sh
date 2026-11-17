#!/usr/bin/env bash
# provision-infra.sh - prepara a MAQUINA (ainda sem a aplicacao): nome, rede, DNS, pacotes,
# usuarios, permissoes, pastas e firewall.
#
# Uso:   sudo ./provision-infra.sh            (config opcional em config/provisionamento.conf)
#        DRY_RUN=1 ./provision-infra.sh       (so mostra o que faria, sem mexer em nada)
#
# Pode ser rodado varias vezes: cada passo confere se ja esta feito antes de agir (idempotente).
set -Eeuo pipefail

DIR_DESTE_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DIR_PROJETO="$(cd "${DIR_DESTE_SCRIPT}/.." && pwd)"
# shellcheck source=lib/comum.sh
source "${DIR_DESTE_SCRIPT}/lib/comum.sh"

iniciar_script "provision-infra"
exigir_root

# ---------- 0. Configuracao (valores padrao; o arquivo opcional sobrescreve) ----------
HOSTNAME_VM="termometro-acs"
DOMINIO_LOCAL="gs.local"
FUSO_HORARIO="America/Sao_Paulo"
REDE_CONFIGURAR=1
REDE_INTERFACE=""
REDE_IP_CIDR="10.0.2.15/24"
REDE_GATEWAY="10.0.2.2"
DNS_SERVIDORES="1.1.1.1 8.8.8.8"
OPERADOR_USUARIO="operador"
PORTA_API=5080
carregar_config "${DIR_PROJETO}/config/provisionamento.conf"

GRUPO_APP="termometro"
USUARIO_APP="termometro"
GRUPO_LOGS="termometro-logs"
USUARIO_MONITOR="monitor"

# ---------- 1. Identidade da maquina ----------
passo_identidade() {
  log INFO "== 1/7 Identidade: hostname, fuso, /etc/hosts =="
  if [[ "$(hostname)" != "$HOSTNAME_VM" ]]; then
    executar "definindo o hostname como ${HOSTNAME_VM}" hostnamectl set-hostname "$HOSTNAME_VM"
  else
    log INFO "hostname ja e ${HOSTNAME_VM}"
  fi
  if [[ "$(timedatectl show -p Timezone --value 2>/dev/null || true)" != "$FUSO_HORARIO" ]]; then
    executar "definindo o fuso horario ${FUSO_HORARIO}" timedatectl set-timezone "$FUSO_HORARIO"
  else
    log INFO "fuso horario ja e ${FUSO_HORARIO}"
  fi
  # Resolucao local: o proprio nome da maquina precisa apontar para ela (sem depender de DNS externo).
  local linha="127.0.1.1 ${HOSTNAME_VM}.${DOMINIO_LOCAL} ${HOSTNAME_VM}"
  if [[ "$DRY_RUN" == "1" ]]; then
    log SIMULADO "garantiria em /etc/hosts a linha: ${linha}"
  elif grep -qE "^127\.0\.1\.1[[:space:]]" /etc/hosts; then
    if ! grep -qxF "$linha" /etc/hosts; then
      cp -a /etc/hosts "/etc/hosts.bak-$(date +%Y%m%d%H%M%S)"
      sed -i -E "s|^127\.0\.1\.1[[:space:]].*|${linha}|" /etc/hosts
      log INFO "/etc/hosts atualizado: ${linha}"
    else
      log INFO "/etc/hosts ja tem: ${linha}"
    fi
  else
    echo "$linha" >> /etc/hosts
    log INFO "/etc/hosts: adicionada a linha ${linha}"
  fi
}

# ---------- 2. Rede (IP fixo via netplan) ----------
descobrir_interface() {
  if [[ -n "$REDE_INTERFACE" ]]; then echo "$REDE_INTERFACE"; return; fi
  { ip -o -4 route show to default 2>/dev/null | awk '{print $5; exit}'; } || true
}

passo_rede() {
  log INFO "== 2/7 Rede: IP, gateway, DNS =="
  if [[ "$REDE_CONFIGURAR" != "1" ]]; then
    log AVISO "REDE_CONFIGURAR=0: mantenho a rede como esta (DHCP ou ja configurada)"
    return 0
  fi
  local iface; iface="$(descobrir_interface)"
  if [[ -z "$iface" ]]; then
    if [[ "$DRY_RUN" == "1" ]]; then iface="enp0s3"; log SIMULADO "sem rota padrao aqui; usaria a interface ${iface}"
    else log ERRO "nao achei a interface de rede. Defina REDE_INTERFACE em config/provisionamento.conf"; return 1; fi
  fi
  log INFO "interface de rede: ${iface}; IP ${REDE_IP_CIDR}; gateway ${REDE_GATEWAY}; DNS ${DNS_SERVIDORES}"

  local dns_lista; dns_lista="$(echo "$DNS_SERVIDORES" | sed -E 's/[[:space:]]+/, /g')"
  escrever_arquivo /etc/netplan/60-termometro.yaml 600 root:root <<YAML
# Gerado por provision-infra.sh. IP fixo para o servidor ter sempre o mesmo endereco.
network:
  version: 2
  ethernets:
    ${iface}:
      dhcp4: false
      addresses: [${REDE_IP_CIDR}]
      routes:
        - to: default
          via: ${REDE_GATEWAY}
      nameservers:
        addresses: [${dns_lista}]
YAML

  if [[ "$DRY_RUN" != "1" ]]; then
    # Outras definicoes (ex.: a do instalador, com DHCP) brigariam com a nossa para a mesma interface.
    local outro
    for outro in /etc/netplan/*.yaml; do
      [[ "$outro" == "/etc/netplan/60-termometro.yaml" || ! -f "$outro" ]] && continue
      if grep -q "$iface" "$outro"; then
        mv "$outro" "${outro}.desativado-pelo-termometro"
        log INFO "desativei ${outro} (renomeado), pois definia a mesma interface"
      fi
    done
  fi
  executar "validando a configuracao de rede" netplan generate
  # "netplan try" pede confirmacao e desfaz sozinho se a conexao cair; sem terminal, aplicamos direto.
  executar "aplicando a configuracao de rede" netplan apply
}

# ---------- 3. DNS ----------
passo_dns() {
  log INFO "== 3/7 DNS: resolucao de nomes =="
  if [[ "$DRY_RUN" == "1" ]]; then
    log SIMULADO "testaria resolver github.com e api.nuget.org"
    return 0
  fi
  if systemctl is-active --quiet systemd-resolved; then
    log INFO "systemd-resolved ativo; servidores DNS em uso:"
    resolvectl dns 2>&1 | sed 's/^/    | /' | tee -a "$LOG_ARQUIVO" >/dev/null || true
  fi
  local alvo ok=1
  for alvo in github.com api.nuget.org; do
    if getent hosts "$alvo" >/dev/null 2>&1; then
      log OK "DNS resolveu ${alvo} -> $(getent hosts "$alvo" | awk '{print $1; exit}')"
    else
      log ERRO "DNS NAO resolveu ${alvo} (sem internet ou DNS errado)"; ok=0
    fi
  done
  if getent hosts "$HOSTNAME_VM" >/dev/null 2>&1; then
    log OK "o nome local ${HOSTNAME_VM} resolve para $(getent hosts "$HOSTNAME_VM" | awk '{print $1; exit}')"
  else
    log ERRO "o nome local ${HOSTNAME_VM} nao resolve"; ok=0
  fi
  [[ "$ok" == "1" ]] || return 1
}

# ---------- 4. Pacotes ----------
passo_pacotes() {
  log INFO "== 4/7 Pacotes base =="
  executar "atualizando a lista de pacotes" env DEBIAN_FRONTEND=noninteractive apt-get update
  garantir_pacotes curl ca-certificates git jq ufw unzip htop stress-ng openssl logrotate postgresql sudo
}

# ---------- 5. Usuarios, grupos e permissoes ----------
garantir_grupo() {
  if getent group "$1" >/dev/null; then log INFO "grupo ja existe: $1"
  else executar "criando o grupo $1" groupadd --system "$1"; fi
}
garantir_usuario_sistema() {  # nome, home, descricao
  if id "$1" >/dev/null 2>&1; then log INFO "usuario ja existe: $1"
  else executar "criando o usuario de sistema $1" useradd --system --gid "$GRUPO_APP" --home-dir "$2" --no-create-home --shell /usr/sbin/nologin --comment "$3" "$1"; fi
}

passo_usuarios() {
  log INFO "== 5/7 Usuarios, grupos e permissoes =="
  garantir_grupo "$GRUPO_APP"
  garantir_grupo "$GRUPO_LOGS"
  garantir_usuario_sistema "$USUARIO_APP" /opt/termometro "Termometro do ACS - roda a API"
  garantir_usuario_sistema "$USUARIO_MONITOR" /var/lib/termometro "Termometro do ACS - monitor"
  executar "colocando ${USUARIO_APP} e ${USUARIO_MONITOR} no grupo de logs" usermod -aG "$GRUPO_LOGS" "$USUARIO_APP"
  executar "colocando ${USUARIO_MONITOR} no grupo de logs" usermod -aG "$GRUPO_LOGS" "$USUARIO_MONITOR"

  # Operador humano: entra por SSH, le logs e liga/desliga SO os servicos do projeto. Sem senha definida aqui:
  # defina com "sudo passwd operador" ou instale uma chave SSH.
  if id "$OPERADOR_USUARIO" >/dev/null 2>&1; then log INFO "usuario ja existe: ${OPERADOR_USUARIO}"
  else executar "criando o usuario operador ${OPERADOR_USUARIO}" useradd --create-home --shell /bin/bash --comment "Operador do Termometro" "$OPERADOR_USUARIO"; fi
  executar "colocando ${OPERADOR_USUARIO} no grupo de logs" usermod -aG "$GRUPO_LOGS" "$OPERADOR_USUARIO"

  # sudo minimo: o operador so mexe nos servicos do projeto; nada de root geral.
  escrever_arquivo /etc/sudoers.d/termometro 440 root:root <<SUDO
# Gerado por provision-infra.sh. Quem pode o que (privilegio minimo).
Cmnd_Alias TERMOMETRO_SERVICOS = /usr/bin/systemctl start termometro-*, /usr/bin/systemctl stop termometro-*, /usr/bin/systemctl restart termometro-*, /usr/bin/systemctl status termometro-*
${OPERADOR_USUARIO} ALL=(root) NOPASSWD: TERMOMETRO_SERVICOS
SUDO
  if [[ "$DRY_RUN" != "1" ]]; then
    visudo -cf /etc/sudoers.d/termometro >/dev/null && log OK "sudoers validado (visudo -c)"
  fi

  # Pastas: cada uma com dono e permissao pensados.
  #   /opt/termometro        codigo publicado    dono root, leitura para o grupo da app
  #   /etc/termometro        configuracao        dono root, so o grupo da app le (tem segredos)
  #   /var/lib/termometro    dados da app        dono da app
  #   /var/log/termometro    logs                grupo de logs escreve, o resto le
  executar "criando /opt/termometro" install -d -m 0755 -o root -g "$GRUPO_APP" /opt/termometro
  executar "criando /etc/termometro" install -d -m 0750 -o root -g "$GRUPO_APP" /etc/termometro
  executar "criando /var/lib/termometro" install -d -m 0750 -o "$USUARIO_APP" -g "$GRUPO_APP" /var/lib/termometro
  executar "criando /var/log/termometro" install -d -m 2775 -o root -g "$GRUPO_LOGS" /var/log/termometro
  if [[ "$DRY_RUN" != "1" ]]; then
    chgrp -R "$GRUPO_LOGS" "$DIR_LOG" 2>/dev/null || true
    chmod 664 "$DIR_LOG"/*.log 2>/dev/null || true
  fi
}

# ---------- 6. Firewall ----------
passo_firewall() {
  log INFO "== 6/7 Firewall (ufw) =="
  executar "politica padrao: negar entrada" ufw default deny incoming
  executar "politica padrao: permitir saida" ufw default allow outgoing
  executar "liberando SSH (22/tcp)" ufw allow 22/tcp
  executar "liberando a API (${PORTA_API}/tcp)" ufw allow "${PORTA_API}/tcp"
  executar "ligando o firewall" ufw --force enable
}

# ---------- 7. Rotacao de logs ----------
passo_logrotate() {
  log INFO "== 7/7 Rotacao de logs =="
  escrever_arquivo /etc/logrotate.d/termometro 644 root:root <<'LR'
/var/log/termometro/*.log {
    weekly
    rotate 8
    compress
    delaycompress
    missingok
    notifempty
    create 664 root termometro-logs
}
LR
}

passo_identidade
passo_rede
passo_dns
passo_pacotes
passo_usuarios
passo_firewall
passo_logrotate
log OK "infraestrutura pronta. Proximo passo: sudo ./provision-app.sh"
