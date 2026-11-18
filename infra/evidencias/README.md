# Evidências (prints)

Salve aqui os prints da VM com estes nomes, na ordem do vídeo. Cada print deve mostrar o comando e a saída.

| Arquivo | O que mostrar |
|---------|---------------|
| `01-hostname-rede.png` | `hostnamectl`, `ip -br addr`, `ip route` |
| `02-dns.png` | `resolvectl status` e `getent hosts github.com` |
| `03-provision-infra.png` | fim da execução de `provision-infra.sh` (linhas `OK`) |
| `04-usuarios-permissoes.png` | `id termometro monitor operador` e `ls -ld /opt/termometro /etc/termometro /var/lib/termometro /var/log/termometro` |
| `05-provision-app.png` | fim da execução de `provision-app.sh` (API respondeu, serviços ativos) |
| `06-servicos-systemd.png` / `06b-timers.png` | `systemctl status termometro-api termometro-monitor termometro-cloudflared` e `systemctl list-timers 'termometro-*'` |
| `07-habilitados-no-boot.png` | `systemctl is-enabled ...` |
| `08-log-monitor.png` | `tail -n 20 /var/log/termometro/monitor.log` |
| `09-api-health.png` | `curl -s http://127.0.0.1:5080/health` |
| `09-api-swagger.png` | Swagger aberto no navegador do Mac (http://192.168.64.2:5080/swagger) |
| `10` a `17` | um print por teste de falha (nomes na tabela de `../testes-de-falha.md`); `14b` mostra os limites restaurados |
| `18-tunel-url.png` | `./scripts/mostrar-url-tunel.sh` |

Dica: no Mac, Cmd+Shift+4 e selecione a janela do terminal. Não mostre o conteúdo de
`/etc/termometro/termometro.env` (tem segredos).
