# API (.NET 10) — C# Software Development + SOA

**Integrante:** Luiz Henrique Dos Reis Grabe — RM 556001

Parte do **Termômetro do ACS**: o ESP32 manda a temperatura e a umidade de dentro da casa de um idoso; a API decide a cor do semáforo e a ordem da fila de visitas do agente comunitário de saúde (ACS). Contrato completo: [`../docs/CONTRATO.md`](../docs/CONTRATO.md).

## Estado

| Parte | Situação |
|---|---|
| `Domain` (regras puras) + testes | ✅ pronto — 58 testes |
| `Infrastructure` (EF Core, PostgreSQL, migrations) | ⏳ próxima |
| `Application` + `Api` (endpoints, Swagger, JWT) | ⏳ depois |

## O que o `Domain` faz

| Classe | Regra |
|---|---|
| `IndiceCalor` | Combina temperatura e umidade (fórmulas do NWS: simples + Rothfusz com ajustes). Função pura. |
| `ClassificadorNivel` | Leitura → nível: azul (< 18 °C), verde, amarelo, vermelho. Em dia de calor do ML, o calor sobe um degrau. |
| `ConfirmadorDeNivel` | Histerese: piora após 3 leituras seguidas, melhora após 6 (o semáforo não pisca). |
| `StatusSinal` | 30 min sem leitura = cinza (sem sinal). |
| `OrdenadorDeFila` | Sem sinal > vermelho (e azul em dia de frio) > azul > amarelo > verde; empate por vulnerabilidade e tempo sem visita. |
| `LimiarTemperatura` | As faixas, validadas em ordem. Ficam no banco, nunca no código. |

Camadas em português, regras só no `Domain`, sem dependência de banco ou relógio (o tempo entra como parâmetro, para o teste controlar).

## Como rodar

```bash
cd api
dotnet test        # roda os 58 testes
```

## Evidências

(a preencher: print do `dotnet test` verde e, depois, do Swagger)
