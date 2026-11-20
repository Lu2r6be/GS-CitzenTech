# Firmware (ESP32 no Wokwi) · Physical Computing: IoT & IoB

**Integrante:** Luiz Henrique Dos Reis Grabe · RM 556001
**Projeto no Wokwi:** https://wokwi.com/projects/480861054313325569
**Vídeo:** https://youtu.be/zbdKyTkFcg4

O ESP32 lê temperatura e umidade (DHT22), envia para a API e acende o LED da cor que a API responder.
A cor é decidida pela API, para que os limiares fiquem num só lugar.

```
DHT22 ──► ESP32 ──HTTPS──► túnel ──► API ──► {"nivel":"amarelo"} ──► LED
```

## Arquivos

| Arquivo | Conteúdo |
|---|---|
| `sketch.ino` | Programa do ESP32. As primeiras constantes configuram URL da API, id, chave e intervalo. |
| `diagram.json` | Circuito: DHT22 no pino 15; LEDs azul 25, verde 26, amarelo 27 e vermelho 14 (resistores de 220 Ω). |
| `libraries.txt` | Biblioteca `DHT sensor library`. |
| `integrantes.txt` | Integrantes. |

## Como rodar

1. Suba a API: `dotnet run --project api/src/Termometro.Api`.
2. Abra um túnel público (o Wokwi não alcança `localhost`): `cloudflared tunnel --url http://localhost:5080`.
3. No Wokwi, crie um projeto ESP32 e use `sketch.ino`, `diagram.json` e `libraries.txt` desta pasta.
4. Coloque o endereço do túnel em `API_BASE` no `sketch.ino` e clique em Play.
5. No monitor serial aparece algo como `31.0 C | 50.0 % -> verde`. Mexa nos controles do DHT22 para mudar a leitura.

A cor só piora depois de 3 leituras seguidas na faixa nova e só melhora depois de 6, para não ficar oscilando.

## Segurança

- O sensor envia apenas `deviceId`, temperatura e umidade; nenhum dado pessoal.
- A chave `X-Device-Key` do sketch é de demonstração.
- Leitura inválida (`NaN`) não é enviada e os LEDs apagam.
- Sem leitura por 30 minutos, a API mostra a casa como "sem sinal" no topo da fila.
- `setInsecure()` é usado só na demonstração (o túnel já usa HTTPS).
