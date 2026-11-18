# API (.NET 10) · C# Software Development e SOA

**Integrante:** Luiz Henrique Dos Reis Grabe · RM 556001

Recebe as leituras do ESP32, classifica cada casa por cor e monta a fila de visitas do agente comunitário de saúde (ACS).
Também cuida do login, do cadastro de moradores com consentimento e do painel Risco Cidadão (módulo de segurança).

## Camadas

```
Domain          regras puras: índice de calor, nível, confirmação (histerese), fila, motor de PII
Application     casos de uso e interfaces (portas) para banco e serviços externos
Infrastructure  EF Core, PostgreSQL/SQLite, repositórios, hash de senha
Api             controllers, JWT, Swagger, tratamento de erros, guardas de log e resposta
```

Conceitos usados: POO, interfaces, injeção de dependência, EF Core com migrations, LINQ, async/await e testes unitários.

## Endpoints

A lista completa e testável está em `/swagger`.

| Método e rota | Perfil | Função |
|---|---|---|
| `POST /api/v1/auth/login` | todos | e-mail e senha → token JWT |
| `POST /api/v1/telemetria` | ESP32 (`X-Device-Key`) | recebe a leitura e devolve o nível |
| `GET /api/v1/config/limiares` | público | faixas de temperatura em vigor |
| `GET /api/v1/visitas/fila` | ACS | casas da microárea, da mais urgente para a menos |
| `POST /api/v1/visitas/{id}/realizar` | ACS | registra uma visita |
| `POST /api/v1/visitas/sincronizar` | ACS | envia visitas feitas sem internet (sem duplicar) |
| `GET /api/v1/domicilios/{id}` | ACS, admin, familiar da casa | resumo e últimas leituras |
| `GET /api/v1/domicilios/{id}/leituras` | idem | histórico (`?de=&ate=`) |
| `GET /api/v1/familiar/casa` | familiar | situação da casa do parente |
| `POST /api/v1/residentes` | ACS, admin | cadastra pessoa, casa e consentimento |
| `POST /api/v1/consentimentos` | ACS, admin | registra novo consentimento |
| `GET /api/v1/seguranca/risco-cidadao` | admin | indicador Risco Cidadão e alerta |
| `POST /api/v1/seguranca/achados` | admin | importa achados do Semgrep/Trufflehog |
| `POST /api/v1/seguranca/analisar-texto` | admin | testa o motor de PII |
| `POST /api/v1/seguranca/simular-vazamento` | admin (demonstração) | mostra os guardas barrando dados pessoais |
| `GET /health` | público | situação da API e do banco |

## Como rodar no computador

```bash
cd api
dotnet run --project src/Termometro.Api
```

Swagger em `http://localhost:5080/swagger`. Em desenvolvimento a API usa SQLite e carrega um cenário de demonstração
com 5 casas fictícias. Contas de teste (senha `Demo@2026!`): `ana.acs@demo.local`, `admin@demo.local`, `neto@demo.local`.
Chave do ESP32 de demonstração: `chave-demo-do-esp32-001`. Para recomeçar do zero, apague `src/Termometro.Api/termometro-dev.db`.

Leitura de teste:

```bash
curl -X POST http://localhost:5080/api/v1/telemetria \
  -H "Content-Type: application/json" -H "X-Device-Key: chave-demo-do-esp32-001" \
  -d '{"deviceId":"esp32-001","temperaturaC":33.4,"umidade":61}'
```

## Como rodar na VM (PostgreSQL)

Configuração por variáveis de ambiente (nenhum segredo fica no repositório):

```
ASPNETCORE_ENVIRONMENT=Production
Banco__Provedor=Postgres
Banco__ConexaoPostgres=Host=localhost;Database=termometro;Username=...;Password=...
Jwt__Chave=<texto aleatório com 32 ou mais caracteres>
MISTRAL_API_KEY=<opcional, para o alerta escrito por IA>
```

As migrations são aplicadas ao subir. O provisionamento automático está em [`../infra`](../infra).

## Testes

```bash
cd api
dotnet test
```

Testes do domínio, da infraestrutura (banco em memória) e da API de ponta a ponta.

## Evidências

- `dotnet test`: 267 testes, 0 falhas → [`prints/01-dotnet-test.png`](prints/01-dotnet-test.png)
- Swagger rodando na VM (`http://192.168.64.2:5080/swagger`) → [`prints/02-swagger.png`](prints/02-swagger.png)
