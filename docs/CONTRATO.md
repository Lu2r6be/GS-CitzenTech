# Contrato técnico — Termômetro do ACS (v2, 09/11/2026)

Este é o documento-mãe. Quem programa: Claude (Claude Code, conversa própria) como principal; GPT só para código pesado pontual. Todo prompt começa com: "Leia docs/CONTRATO.md e siga-o". Se a IA discordar do contrato, ela PERGUNTA, não inventa. Mudou o contrato? Atualize este arquivo primeiro.

Analogia: o contrato é a planta da casa. Cada IA é um pedreiro; sem planta, as paredes não se encontram.

## 1. Visão geral (o caminho do dado)
```
ESP32 (Wokwi, DHT22)            API C# / .NET            App Expo (ACS / Familiar)
 lê temperatura+umidade  ──►  valida, calcula índice  ──►  fila de visitas por risco
 envia a cada N min           de calor, aplica limiares     detalhe da casa, registra visita
        ▲                      grava no PostgreSQL                  ▲
        │ GET /config/limiares   ▲        ▲                        │
        └────────────────────────┘        │                        │
                           Notebook ML ───┘ (gera limiares e "dia de risco" com INMET + SIM)
                           Módulo de Segurança (PII + taint + LLM) ──► Dashboard "Risco Cidadão"
VM Linux: provisiona, hospeda a API, monitora (CPU/disco/memória), systemd
```

## 2. Decisões (o que e por quê)
- **Banco:** PostgreSQL na VM (o provisionamento instala e configura → pontos de OS; migrations do EF Core → pontos de SOA).
- **Backend:** .NET (C#), arquitetura em camadas: `Domain` (regras puras, sem dependência), `Application` (casos de uso, interfaces), `Infrastructure` (EF Core, repositórios), `Api` (controllers, DI, Swagger). Async/await em tudo que toca banco; LINQ nas consultas.
- **O ESP32 manda DADO BRUTO** (temperatura, umidade, id do dispositivo). Quem classifica é a API. Assim os limiares ficam em um só lugar e o ML consegue alterá-los.
- **O dispositivo não manda PII.** Ele só conhece um `deviceId` e um token. A ligação deviceId → casa existe só no banco.
- **VM:** VirtualBox no PC (sem Azure for Students). O Wokwi só alcança URL pública → túnel `cloudflared` rodando como serviço systemd, provisionado pelo script.
- **O semáforo é honesto:** `nível = f(índice de calor interno, alerta externo do dia)`. A **vulnerabilidade** (idade ≥ 80, mora sozinho, doença crônica) NÃO muda a cor: só a **ordem da fila**. A cor é "o que o sensor mede".
  - Índice de calor interno: fórmula de Rothfusz (função pura em `Domain`, com testes). Ela só vale a partir de ~27 °C (80 °F); abaixo disso, usar a fórmula simples do NWS.
  - **Confirmação (histerese):** o nível só SOBE após 3 leituras seguidas na faixa nova e só DESCE após 6. Evita o semáforo piscando (como um termostato que não liga e desliga a cada décimo de grau).
  - **Sem sinal:** sem leitura há 30 min → status **cinza "sem sinal"**, que vai para o TOPO da fila. Sensor mudo nunca pode parecer "seguro".
  - **Exibição gradual:** o app mostra a temperatura numa barra de cor contínua (gradiente verde→amarelo→vermelho) com seta de tendência (subindo/estável/descendo, últimos 60 min). A DECISÃO (fila, alerta) usa os níveis discretos; o gradiente é para leitura humana.
  - Faixas iniciais (a verificar antes de citar): verde < 32 °C; amarelo 32–39 °C; vermelho ≥ 39 °C (baseadas nas faixas do NWS, feitas para adultos saudáveis — para idosos talvez devam ser mais baixas; buscar fonte). Ficam na tabela `LimiarCalor`, nunca no código.
  - "Dia de risco" vem do ML. Pergunta do ML: *dado o clima externo dos últimos dias (máxima, mínima, umidade, lags 0–7), quantos óbitos de 60+ são esperados no dia em SP?* (XGBoost ou Random Forest, regressão). Dia de risco = óbitos previstos acima de um corte (ex.: percentil 90 do histórico); esse corte, traduzido em temperatura máxima externa, vai para `DiaRisco`/`LimiarCalor`. Se hoje é dia de risco, o nível sobe um degrau.
  - O clima externo de hoje vem de uma tabela `ClimaExterno` (última observação do INMET, carregada por script ou à mão na demo). Buscar o INMET em tempo real é opcional e está nas questões abertas.
  - Flag "noite sem resfriamento": mínima interna noturna acima de um valor configurável (a calibrar, citar fonte).
  - O ML NÃO mede a casa. Ele diz "a que temperatura EXTERNA os óbitos 60+ sobem". A casa é medida pelo sensor.

## 3. Entidades (tabelas)
| Entidade | Campos principais | PII? |
|---|---|---|
| Residente | id, nome, cpf, dataNascimento, condicoesSaude (texto), telefoneContato | SIM (nome, CPF, saúde) |
| Residente (vulnerabilidade) | moraSozinho, temDoencaCronica (bool; usados só na ordem da fila) | SIM (saúde) |
| Domicilio | id, residenteId, microareaId, endereco, cep, tipoCobertura (laje/telha/fibrocimento), andar, statusSinal | SIM (endereço) |
| Dispositivo | id, domicilioId, deviceKeyHash, ativo | não |
| Leitura | id, dispositivoId, timestampUtc, temperaturaC, umidade, indiceCalorC, nivel | não |
| Microarea | id, nome, acsId | não |
| Usuario | id, nome, email, senhaHash, papel (Acs/Familiar/Admin), microareaId? (ACS), residenteId? (Familiar) | SIM (email, senha) |
| ClimaExterno | id, estacao, dataHoraUtc, tempMaxC, tempMinC, umidade | não |
| DiaRisco | id, data, estacao, obitosPrevistos, corte, modeloVersao, nivelExterno | não |
| Consentimento | id, residenteId, finalidade, concedidoEm, revogadoEm | não |
| Visita | id (GUID gerado no app), domicilioId, acsId, planejadaEm, realizadaEm, observacao, resultado, origem (Online/Offline), sincronizadaEm | observação pode ter saúde |
| LimiarCalor | id, versao, verdeAte, amareloAte, origem (Regra/ML), criadoEm | não |
| AchadoSeguranca | id, tipo, severidade, origem, exposicao, categoriaPii, criadoEm | não |

## 4. Mensagem do ESP32 (JSON)
`POST /api/v1/telemetria` — header `X-Device-Key: <segredo>`
```json
{ "deviceId": "esp32-001", "temperaturaC": 33.4, "umidade": 61.0 }
```
A hora é registrada pela API no recebimento (o ESP32 não precisa de relógio/NTP — um ponto de falha a menos).
Resposta: `202 Accepted` com `{ "nivel": "amarelo" }` (o ESP32 acende o LED correspondente).
`GET /api/v1/config/limiares` (o ESP32 lê na inicialização).

## 5. Endpoints (v1)
- `POST /api/v1/auth/login` → JWT (papéis Acs, Familiar, Admin)
- `GET /api/v1/visitas/fila` (Acs) → só a microárea do ACS; ordem: sem sinal > vermelho > amarelo > verde; empate por vulnerabilidade e tempo desde a última visita
- `POST /api/v1/visitas/sincronizar` (Acs) → recebe lote de visitas feitas offline; idempotente pelo GUID
- `GET /api/v1/domicilios/{id}` → resumo e últimas leituras
- `GET /api/v1/domicilios/{id}/leituras?de=&ate=`
- `POST /api/v1/visitas/{id}/realizar` (Acs)
- `GET /api/v1/familiar/casa` (Familiar) → status simples da casa vinculada
- `POST /api/v1/residentes` e `POST /api/v1/consentimentos` (Admin/Acs; exigem consentimento)
- `GET /api/v1/config/limiares`
- `GET /api/v1/seguranca/risco-cidadao` (Admin) → indicador e achados
- `GET /health`
Regras: nenhuma resposta traz CPF completo (mascarar `***.456.789-**`); nenhum log traz PII.

## 6. Cibersegurança (como encaixa no código)
- Campos de PII marcados com um atributo (`[Pii(Categoria.Cpf)]`) → fonte para o **parsing** e para o **taint analysis**.
- Fontes (entradas que carregam PII): corpo das requisições, banco. Sumidouros (saídas perigosas): logs, respostas HTTP, prompt do LLM, arquivos. Sanitizador: `PiiSanitizer`.
- Cada bloqueio gera um `AchadoSeguranca`. O **Risco Cidadão** = combinação de severidade x categoria x exposição dos achados (fórmula documentada).
- O LLM (**Mistral API**, crédito de US$ 30) recebe só o achado técnico já sanitizado e devolve um alerta em linguagem simples. Chave em variável de ambiente (`MISTRAL_API_KEY`), nunca no repositório. Se a API falhar, usar um texto-modelo de reserva.
- **Ferramentas (inferência a partir das aulas [13] DevSecOps e da entrega da Sprint 3; confirmar com o professor):**
  - Regras PII como **regras customizadas do Semgrep** (CPF, saúde, senha, chave) + **Semgrep em modo taint** (sources: campos `[Pii]`/corpo da requisição; sinks: `ILogger`, respostas HTTP, cliente do LLM; sanitizer: `PiiSanitizer`).
  - Parser em tempo de execução (middleware da API): regex com validação de dígito do CPF antes de qualquer log.
  - **Trufflehog** em pre-commit e no GitHub Actions (segredos, ex.: `MISTRAL_API_KEY`).
  - Um script lê o JSON do Semgrep/Trufflehog → grava `AchadoSeguranca` → Mistral traduz → dashboard calcula o Risco Cidadão.
- Pergunta aberta: existe código-base de "motor de parsing"? As aulas 09–12 não estão no material recebido.

## 7. Estrutura do repositório (monorepo)
```
/api        Domain, Application, Infrastructure, Api, Tests (xUnit)
/app        Expo + TypeScript (Expo Router) — telas: login, fila, casa, consentimento, familiar
            modo offline: visitas salvas em SQLite local (expo-sqlite) e sincronizadas quando houver rede
/firmware   projeto Wokwi (sketch + diagram.json)
/ml         notebook, dados/README.md, requirements.txt, RELATORIO_ML.md
/security   módulo PII/taint, dashboard, relatório de auditoria
/infra      provision-infra.sh (hostname, IP, DNS, usuários/permissões), provision-app.sh, monitor.sh,
            units systemd (app, monitor com timer/Restart=, cloudflared), logs/, evidencias/ (prints), testes-de-falha.md
/docs       CONTRATO.md, GLOSSARIO.md, ficha-citizentech.md, rubricas.md
CLAUDE.md   instruções principais (Claude Code) → aponta para docs/CONTRATO.md
AGENTS.md   cópia curta para o GPT
README.md   índice: uma linha por disciplina com o link da pasta (ex.: .../tree/main/ml)
```
Um único repositório e uma única branch `main`. NÃO separar por branches: cada branch esconderia o resto do projeto e as partes deixariam de "conversar". Para cada professor, enviar o link da pasta da disciplina. Se precisar "congelar" a versão entregue, usar uma tag (ex.: `entrega-19-11`).

## 8. Convenções de código (para ficar entendível)
- Nomes de domínio em português; termos técnicos em inglês só onde a plataforma exige.
- Funções pequenas, uma responsabilidade; regras de negócio só em `Domain`; sem lógica nos controllers.
- Interfaces + injeção de dependência (pontos de C#). Async/await sem `.Result`.
- Cada regra do `Domain` tem teste. Nome do teste diz o que acontece: `IndiceCalor_Acima39_RetornaVermelho`.
- Comentário explica o POR QUÊ, não o quê. README por pasta em 5 linhas.
- Commits pequenos: uma tarefa por commit.

## 9. Como pedir às IAs (para você entender o que sai)
1. Um pedido = uma tarefa (ex.: "implemente a entidade Leitura, repositório e migration").
2. Termine todo prompt com: "Explique em 5 linhas o que fez, por que, e dê uma analogia. Liste o que eu devo conferir."
3. Rode e leia o diff antes do commit. Se não entender uma parte, pergunte "por que isso existe?".
4. Ao terminar cada módulo, escreva (ou peça) 3 linhas no `docs/GLOSSARIO.md`: o que é, onde está, como testar.
5. O que a IA não resolve: dados reais, ambiente, integração do Wokwi e a entrevista.

## 10. Ordem de construção sugerida
1. Contrato + repo + CLAUDE.md/AGENTS.md. 2. `checar_dados.py` e escolha da estação. 3. Domain + testes (índice de calor, nível). 4. API + EF + migrations + Swagger. 5. Wokwi → API. 6. ML + limiares. 7. App. 8. VM/OS. 9. Segurança/Risco Cidadão. 10. Azure Boards, vídeos, pitch.
Sobrando tempo: refinar o score (por que uma casa é mais perigosa), UX do ACS, entrevista com ACS, qualidade de código.

## 10b. Cenário de demonstração (decidir cedo, senão a demo não funciona)
- Uma microárea com um ACS e 4–5 casas. UMA casa tem o ESP32 real do Wokwi (a avó, no cenário contado no pitch).
- As outras casas são alimentadas por um script `simulador_casas` (Python) que envia leituras fictícias para a API. Esses dados existem só para a DEMO do app e nunca entram no ML (a rubrica proíbe dados simulados no ML). Declarar isso no vídeo.
- A fila do ACS mostra as 5 casas ordenadas por nível; o ACS registra uma visita e a casa sai do topo.
- Roteiro do pitch: a avó → o sensor → o semáforo → a fila → a visita.

## 11. Questões abertas
- Clima externo: carga manual do INMET na demo ou busca automática?
- Motor de parsing de Cyber: é código-base de aula? (Fenyknit vai verificar.)
- Confirmar com os professores que um repositório único com link por pasta é aceito.
- Limiares (fonte), "noite sem resfriamento" (valor e fonte), estação INMET, colunas do SIM.
- TLS do Wokwi (HTTPS) e como expor a API da VM ao simulador.
