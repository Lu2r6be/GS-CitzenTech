# Testes de falha

Cada teste tem: o que fazer, o que deve acontecer e onde olhar. Marque o resultado na tabela do fim e salve
o print na pasta `evidencias/` com o nome sugerido.

Antes de começar, abra **dois terminais** na VM (ou duas janelas SSH). No segundo, deixe rodando:

```bash
tail -f /var/log/termometro/monitor.log /var/log/termometro/guardiao.log
```

## T1. Parar o serviço de monitoramento e ver a recuperação

```bash
sudo systemctl stop termometro-monitor        # parada "pedida": o Restart= não religa de propósito
systemctl is-active termometro-monitor        # inactive
# aguarde até 45 s
systemctl is-active termometro-monitor        # active
```

Esperado no `guardiao.log`: `ALERTA termometro-monitor.service: esta parada` seguido de
`RECUPERACAO termometro-monitor.service voltou a funcionar`.

Variante, matar o processo (queda não planejada), sem a ajuda do guardião:

```bash
sudo kill -9 "$(systemctl show -p MainPID --value termometro-monitor)"
sleep 4; systemctl is-active termometro-monitor     # active (o próprio systemd religou em 2 s)
journalctl -u termometro-monitor -n 15 --no-pager   # mostra a morte e o novo início
```

## T2. Derrubar a API e ver a recuperação

```bash
sudo kill -9 "$(systemctl show -p MainPID --value termometro-api)"   # queda não planejada
sleep 5; curl -s http://127.0.0.1:5080/health                        # Healthy
sudo systemctl stop termometro-api                                   # parada por comando
# aguarde até 45 s
curl -s http://127.0.0.1:5080/health                                 # Healthy de novo
```

Esperado: `Restart=always` religa o `kill -9` em 3 s; o guardião religa o `stop` e registra `RECUPERACAO`.
O monitor, no meio do caminho, registra `ERRO FALHA: a API nao respondeu`.

## T3. Estresse de CPU

```bash
sudo ./testes/estressar.sh cpu 90
```

Esperado no `monitor.log`: `ALERTA CPU em NN% (limite 85%)` em pelo menos um ciclo. Depois do teste os
valores voltam ao normal.

## T4. Estresse de memória

```bash
sudo ./testes/estressar.sh memoria 90
```

Esperado: `ALERTA RAM em NN% (limite 85%)`. Se a VM tiver pouca RAM e o alerta não aparecer, baixe o limite
para o teste: edite `RAM_LIMITE=30` em `/etc/termometro/monitor.conf` (e volte depois).

## T5. Disco

```bash
sudo ./testes/estressar.sh disco 4096
```

Esperado: `ALERTA DISCO em NN%`. Ajuste o tamanho (em MB) ou o `DISCO_LIMITE` conforme o espaço da VM.
O arquivo de teste é apagado ao final.

## T6. Reiniciar a VM

```bash
uptime -s; sudo reboot
# depois de voltar:
uptime -s
systemctl is-active postgresql termometro-api termometro-monitor termometro-guardiao.timer termometro-cloudflared
curl -s http://127.0.0.1:5080/health
```

Esperado: tudo `active` sem ninguém ter ligado nada à mão; o primeiro ciclo do monitor aparece no log
logo após o boot.

## T7. Falha controlada em um script

```bash
./testes/falha-controlada.sh; echo "codigo de saida: $?"
tail -n 8 /var/log/termometro/teste-falha-controlada.log
```

Esperado: código de saída diferente de 0 e as linhas `ERRO comando falhou`, `ERRO falhou na linha` e
`ERRO fim COM ERRO` no log.

## T8. Modo manutenção (o guardião não atrapalha uma parada planejada)

```bash
sudo touch /etc/termometro/manutencao
sudo systemctl stop termometro-api
sleep 70; systemctl is-active termometro-api       # inactive; o guardião só registrou AVISO
sudo rm /etc/termometro/manutencao
# em até 45 s o guardião volta a religar
```

## Resultados (VM no UTM, Ubuntu 26.04 ARM, 17 e 18/11/2026)

| Teste | Data/hora | Resultado esperado ocorreu? | Print |
|-------|-----------|-----------------------------|-------|
| T1 parar o monitor | 17/11 23:08 | Sim. Guardião religou em ~18 s (ALERTA + RECUPERACAO) | `evidencias/10-t1-monitor-recuperado.png` |
| T2 derrubar a API | 17/11 23:23 | Sim. `kill -9` voltou sozinho; `stop` religado pelo guardião | `evidencias/11-t2-api-recuperada.png` |
| T3 CPU | 17/11 23:26 | Sim. `ALERTA CPU em 100%` | `evidencias/12-t3-alerta-cpu.png` |
| T4 memória | 17/11 23:36 | Sim, com limite de teste 50% (a RAM só chegou a 67%) | `evidencias/13-t4-alerta-ram.png` |
| T5 disco | 17/11 23:40 | Sim, com limite de teste 40% e arquivo de 2 GB (`ALERTA DISCO em 44%`) | `evidencias/14-t5-alerta-disco.png` |
| T6 reboot | 18/11 00:01 | Sim. 5 serviços `active` e API `Healthy` sem intervenção | `evidencias/15-t6-reboot.png` |
| T7 falha controlada | 18/11 00:03 | Sim. Código 2 e as três linhas de ERRO | `evidencias/16-t7-falha-controlada.png` |
| T8 manutenção | 18/11 00:07 | Sim. Guardião só avisou; religou ao tirar a placa | `evidencias/17-t8-manutencao.png` |

## O que os testes revelaram (e foi corrigido)

- **Monitor não lia a própria configuração.** Ele roda como usuário `monitor`, sem acesso à pasta
  `/etc/termometro` (0750). Usava os limites padrão sem avisar. Correção: a pasta virou 0751
  (atravessar sim, listar não); o arquivo de segredos continua 640. Achado no T4.
- **sudo novo (sudo-rs) não aceita curinga nos argumentos.** A regra do `operador` passou a listar cada
  serviço; se a regra vier inválida, o script apaga o arquivo e para.
- **`/tmp` em RAM (814 MB)** não cabia o .NET descompactado: o instalador usa `/var/tmp`.
- **PostgreSQL instalado sem cluster.** O script cria o cluster se faltar e espera o banco aceitar conexão.
