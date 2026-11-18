# Infraestrutura (VM Linux) · Operating Systems

**Integrante:** Luiz Henrique Dos Reis Grabe · RM 556001

Scripts que preparam um Ubuntu Server do zero e deixam a API rodando como serviço, com monitoramento,
logs de auditoria e recuperação automática.

## Estrutura

```
scripts/
  provision-infra.sh      hostname, rede, DNS, pacotes, usuários, permissões, firewall
  provision-app.sh        .NET, PostgreSQL, publicação da API, variáveis de ambiente, serviços, túnel
  monitor.sh              CPU, RAM e disco com alerta acima do limite; confere serviços e /health
  guardiao.sh             religa serviços parados e reinicia a API se o /health falhar
  mostrar-url-tunel.sh    URL pública atual do túnel
  coletar-evidencias.sh   junta logs e estado do sistema
  lib/comum.sh            log com data/hora e tratamento de erro
systemd/                  units da API, do monitor, do guardião (serviço + timer) e do túnel
config/                   exemplos de configuração
testes/                   estressar.sh e falha-controlada.sh
logs/                     logs coletados da VM
evidencias/               prints
testes-de-falha.md        roteiro e resultados dos testes de falha
```

## Preparar a VM

Usei o **UTM** num Mac com Apple Silicon. O VirtualBox no Windows também serve: só mude o IP no `provisionamento.conf`.

1. Ubuntu Server ARM64 (testado no 26.04 LTS), 2 CPUs, 2 GB de RAM, 22 GB de disco, com OpenSSH.
2. Rede "Shared Network" do UTM: a VM fica em `192.168.64.2`, acessível pelo Mac (SSH na 22, API na 5080).
3. Se o instalador deixou parte do disco sem uso: `sudo lvextend -r -l +100%FREE /dev/ubuntu-vg/ubuntu-lv`.

## Instalar

```bash
sudo apt-get update && sudo apt-get install -y git
git clone https://github.com/Lu2r6be/GS-CitzenTech.git termometro
cd termometro/infra
cp config/provisionamento.conf.example config/provisionamento.conf
# no UTM: REDE_IP_CIDR="192.168.64.2/24" e REDE_GATEWAY="192.168.64.1"

sudo ./scripts/provision-infra.sh
sudo ./scripts/provision-app.sh
```

`DRY_RUN=1` mostra o que seria feito sem alterar nada. Os scripts podem ser executados de novo: só mudam o que falta.

Do Mac: `ssh lu2r6be@192.168.64.2` e Swagger em `http://192.168.64.2:5080/swagger`.

## Resultado

Os 8 testes de falha passaram; tabela, datas e o que eles revelaram em [`testes-de-falha.md`](testes-de-falha.md).
Prints em `evidencias/` e logs reais da VM em `logs/logs-da-vm/`.

## Operação

| Ação | Comando |
|---|---|
| Situação dos serviços | `systemctl status termometro-api termometro-monitor termometro-cloudflared postgresql` |
| Log do monitor | `tail -f /var/log/termometro/monitor.log` |
| Log do guardião | `cat /var/log/termometro/guardiao.log` |
| Log da API | `journalctl -u termometro-api -f` |
| Parar, ligar ou reiniciar a API | `sudo systemctl stop\|start\|restart termometro-api` |
| URL do túnel | `./scripts/mostrar-url-tunel.sh` |
| Modo manutenção (guardião não religa) | `sudo touch /etc/termometro/manutencao` |
| Limites de alerta | `/etc/termometro/monitor.conf` |

## Requisitos da tarefa

| Requisito | Onde |
|---|---|
| Hostname, rede, DNS, dependências, usuários e permissões | `provision-infra.sh` |
| Instalação da aplicação, variáveis de ambiente, serviço e início automático | `provision-app.sh`, `config/` |
| Monitoramento de CPU, RAM e disco com alertas | `monitor.sh` |
| Serviço systemd tolerante a falhas | `termometro-monitor.service` (`Restart=always`) e `termometro-guardiao.timer` |
| Logs de auditoria | `/var/log/termometro/*.log` (níveis INFO, CMD, ALERTA, ERRO, RECUPERACAO) e `journalctl` |
| Testes de falha | `testes-de-falha.md` e `testes/` |

## Recuperação

- API e monitor têm `Restart=always`: se o processo morrer, o systemd religa.
- O guardião roda a cada 30 s e cobre o que o `Restart=` não cobre: serviço parado por comando e API que não responde
  ao `/health` por 3 checagens seguidas.
- Todos os serviços estão habilitados no boot.

## Segurança

- Senha do banco e chave do JWT são geradas na instalação e ficam em `/etc/termometro/termometro.env` (modo 640;
  a pasta é 751: o monitor atravessa para ler `monitor.conf`, mas não lista nem lê o `.env`).
- A API roda com usuário próprio, sem login, com `ProtectSystem=strict` e `NoNewPrivileges`.
- O usuário `operador` só controla os serviços `termometro-*` via sudo (cada comando listado, sem curinga).
- Firewall `ufw`: entrada liberada só nas portas 22 e 5080.
