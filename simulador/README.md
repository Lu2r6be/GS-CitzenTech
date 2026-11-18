# Simulador das casas (demonstração)

**Integrante:** Luiz Henrique Dos Reis Grabe — RM 556001

A casa da avó tem o ESP32 de verdade (Wokwi). As **outras 4 casas** da microárea são simuladas por este script, para a fila do ACS ter uma casa de cada cor na demonstração.

> **Os dados são FICTÍCIOS e existem só para a demo do app. Nunca entram no ML** (a rubrica de IA exige dados reais). Declare isso no vídeo.

## O que cada casa mostra (cenário `estavel`)

| Dispositivo | Casa | Temperatura | Cor |
|---|---|---|---|
| `sim-002` | José | ≈ 40 °C | vermelho |
| `sim-003` | Antônia | ≈ 33 °C | amarelo |
| `sim-004` | Benedito | ≈ 25 °C | verde |
| `sim-005` | Célia | ≈ 15 °C | azul |

Cenários: `estavel`, `onda-de-calor` (tudo sobe ~0,6 °C por rodada, até +8 °C) e `onda-de-frio` (desce). Lembre da histerese: a cor só muda depois de 3 leituras seguidas na faixa nova.

## Como rodar

Não precisa instalar nada (só o Python 3 que já vem no Mac).

```
cd simulador
export DEVICE_KEY=chave-demo-do-esp32-001
python3 simulador_casas.py --url http://localhost:5080 --intervalo 10
```

Opções: `--cenario onda-de-calor`, `--rodadas 5` (para sozinho; padrão 0 = até Ctrl+C), `--semente 1` (repete o mesmo "ruído"). A chave vem da variável `DEVICE_KEY` (nunca fica no código). Com a API atrás de um túnel, use `--url https://seu-tunel.trycloudflare.com`.

Para mostrar **sem sinal**: pare o script e espere 30 min (a casa vai para cinza no topo da fila). Isso também está coberto por teste automático na API.

## Testes

```
cd simulador
python3 -m unittest
```
