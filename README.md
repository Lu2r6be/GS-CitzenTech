# Termômetro do ACS

Global Solution 2026.2 · CitizenTech · FIAP
**Integrante:** Luiz Henrique Dos Reis Grabe · RM 556001

Um sensor (ESP32) mede a temperatura e a umidade dentro da casa de pessoas idosas. A API classifica a casa por cor
(azul: frio perigoso; verde; amarelo; vermelho: calor perigoso; cinza: sem sinal) e o app mostra ao agente comunitário
de saúde (ACS) uma fila de visitas, das casas mais perigosas para as menos.

## Entregas por disciplina

| Disciplina | Pasta |
|---|---|
| C# Software Development e SOA | [`api/`](api) |
| Mobile Development & IoT | [`app/`](app) |
| Physical Computing (ESP32/Wokwi) | [`firmware/`](firmware) |
| IA & Machine Learning | [`ml/`](ml) |
| Operating Systems | [`infra/`](infra) |
| Cybersecurity | [`security/`](security) |
| Testing (Azure Boards) | https://dev.azure.com/lugrabe26/Termometro-do-ACS |

Outros documentos: [`docs/ficha-citizentech.md`](docs/ficha-citizentech.md) e [`docs/entrevista.md`](docs/entrevista.md).

## Como as partes se ligam

```
ESP32 (firmware) ──► API .NET (api) ──► App Expo (app)
                       │   ▲
          PostgreSQL ◄─┘   └── limiares de "dia de risco" (ml)
VM Linux com systemd e monitoramento (infra) · proteção de dados pessoais (security)
```
