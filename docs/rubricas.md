# Rubricas e entregas por disciplina — GS CitizenTech 2026.2 (lidas do briefing, slides 26–40)

Prazo de TODAS: 19/11/2026 (briefing diz 23h55; tarefas no Teams dizem 23h59 — mirar no mais cedo), via Teams. Em várias, "um único integrante" envia o link.

## Cybersecurity (10 pts) — entrega: ZIP
Missão: (01) Mapeamento de PII: configurar novas regras no **motor de parsing** para detectar dados sensíveis (CPF, saúde, senhas); (02) Taint Analysis: rastrear o fluxo das informações e bloquear vazamentos em saídas inseguras; (03) Tradução via IA: usar um LLM para traduzir falhas técnicas em alertas claros de risco real ao cidadão.
Entregas: código do novo módulo (regras PII), auditoria do protótipo (evidência do taint analysis em ação), dashboard atualizado (vídeo + print do painel gerando o indicador **Risco Cidadão**).
Pontos: robustez do motor de parsing PII 3,0; eficácia do taint analysis 3,0; inovação com IA na remediação 2,0; dashboard executivo (Risco Cidadão) 2,0.
ATENÇÃO: "Risco Cidadão" é o indicador de segurança (risco ao cidadão por falhas/vazamentos de dado), NÃO o risco térmico. Dúvida aberta: o "motor de parsing" é código-base dado em aula?

### Contexto das aulas de Cyber (material recebido em 10/11: aulas 01–08, 13–18 e "Entregas Esperadas – Sprint 3")
- Aula 13 DevSecOps: Trufflehog (regex + entropia, pre-commit hook x Actions), SAST com Semgrep no GitHub Actions bloqueando deploy.
- Aula "[15] Semgrep" na verdade cobre iptables + Suricata (IDS/IPS) com Docker.
- Sprint 3 pediu: Semgrep + Trufflehog com diagrama CI/CD; criptografia local, hardening de API (rate limit, validação, JWT seguro), RBAC; logs estruturados, alertas, resposta a incidentes (SANS PICERL); OWASP Top 10, API Top 10, Mobile Top 10, ASVS. Nenhum material fala em "motor de parsing"; as aulas 09–12 não vieram.
- Leitura provável: o "motor de parsing" = regras customizadas (Semgrep) + parser próprio de PII; o taint = Semgrep taint mode. Confirmar.

## IA & Machine Learning (10 pts) — GitHub + vídeo
Dados REAIS e PÚBLICOS (INMET, Metrô SP, Infosiga SP, SP156, IBGE...). Dados simulados NÃO são aceitos.
Modelo (um): XGBoost, Random Forest, MLP, LSTM ou KMeans+PCA. Testar ≥10 combinações de 3 hiperparâmetros, validação cruzada só no treino; métricas treino x teste lado a lado, gráficos, discutir overfitting; concluir o que significa para o cidadão.
Entrega: pasta `ml/` com notebook executado, `dados/README.md`, `requirements.txt`; `RELATORIO_ML.md` (2 a 4 páginas); vídeo até 5 min no YouTube (não listado).
Pontos: problema e dados reais 2; escolha justificada do modelo 1; preparação e divisão treino/teste 2; treino com parâmetros diferentes 2; resultados treino/teste 2; conclusão e leitura para o cidadão 1.

## Mobile Development & IoT (10 pts) — GitHub + vídeo
App Expo (React Native + TypeScript, Expo Router) que consome API ou mock, **salva em banco de dados**, simples e acessível. Entrega: código organizado, README com a **Ficha da CitizenTech**, instruções e prints; vídeo ≤3 min com o app rodando. 6 critérios: telas e navegação, consumo de dados, banco de dados, acessibilidade, aderência ao problema, documentação.

## C# Software Development (10 pts) — GitHub (texto da tarefa no Teams, 10/11)
Prazo: 19/11, 23h59. Envio só do link do GitHub, por um integrante.
Conteúdos que precisam aparecer: POO, Interfaces, Injeção de Dependência, EF Core, LINQ, API REST (Web API ou Minimal API), Async, **Testes Unitários**.
README obrigatório com: nome e RM dos integrantes; descrição da solução; evidências de funcionamento. Documentação dos endpoints (Swagger ou Scalar). Banco de livre escolha.
Pontos: POO 3,0; Web API 2,5; EF Core + LINQ 1,5; Async/Await 1,0; qualidade do código 2,0.

## SOA e Web Services (10 pts) — GitHub
Backend em qualquer linguagem; README, Swagger, evidências. Estrutura do domínio 3,0; conexão com banco 3,0; migrações 1,5; documentação automática 1,5; testes automatizados 1,0.

## Operating Systems — texto da tarefa no Teams (10/11) — prazo 19/11, 23h59
Opções: WSL (com TODAS as configurações), VM local ou Azure. Incluir o nome de todos os integrantes.
Ambiente (por script, sem configuração manual): hostname; IP e parâmetros de rede; DNS e resolução de nomes; dependências; **usuários e permissões**; preparação para a aplicação Web.
Script de provisionamento da app: pacotes, componentes, diretórios, variáveis de ambiente, permissões, serviço que executa a app, inicialização automática.
Monitoramento (Bash): CPU, RAM, disco, alertas acima de limites. Serviço systemd: inicia no boot, roda periodicamente, registra execuções, identifica falhas, start/stop/restart, tolerante a falhas (recupera após interrupção).
Logs e auditoria: execução dos scripts, instalações/configurações, verificações, alertas, erros, evidências de recuperação.
Testes de falha (exemplos): parar o serviço de monitoramento e ver recuperar; simular CPU/memória alta (ex.: stress); reiniciar a VM; falha controlada num script registrada no log.
Entregas: GitHub (scripts, unidades systemd, logs, README com instalação/execução/reprodução); vídeo ≤7 min (ambiente, provisionamento, app funcionando, monitoramento, systemd, logs, falha e recuperação); **evidências (prints) organizadas no repositório** (ex.: `infra/evidencias/`).

## Operating Systems (10 pts) — resumo do briefing — GitHub + vídeo ≤7 min
Linux (VM) provisionado automaticamente por Shell Script para executar uma aplicação WEB da CitizenTech hospedada. Pontos: script de provisionamento de infra (rede, DNS, IP) 2; script de provisionamento da aplicação (dependências, configuração, orquestração) 2; script de monitoramento com alertas de CPU, disco e memória 2; monitoramento como serviço tolerante a falhas (systemd) 1; logs de auditoria e execução 1; funcionamento completo 2. Também: teste de falha; hostname, auditoria.

## Physical Computing: IoT & IoB
ESP32 no Wokwi integrado à solução (não pode ser projeto paralelo). Entrega: link do projeto Wokwi; vídeo ≤4 min demonstrando a integração; arquivo .txt com os nomes dos integrantes.

## Testing, Compliance & QA — tarefa no Teams (10/11)
"Realizar atividade conforme pdf anexo" (o PDF da tarefa ainda não foi enviado — pode diferir do briefing) + vídeo de apoio "Caso de teste – Azure DevOps" (aula de criação de casos de teste manuais). Nota não lançada no Teams. Prazo 19/11, 23h59.

## Testing (resumo do briefing)
Azure Boards: Features com descrição e critérios de aceite; Test Cases funcionais (positivos, negativos, caixa-preta) com objetivo, preparação, inputs/outputs esperados e passos. Incluir o professor como 'Admin' e colocar o link do projeto Azure no PDF; prints de todas as telas de Features e Test Cases num documento → PDF. Features 50%, Test Cases 50%.

## Pitch
Até 4 min (problema, solução, quem ela ajuda). Ficha da CitizenTech responde 6 perguntas: cidadão, problema, como funciona, tecnologias, ecossistema, impacto medido/ampliado.

## Resumo de entregas pesadas
~6 vídeos (pitch 4 min, ML 5 min, Mobile 3 min, OS 7 min, Physical 4 min, Cyber) + ZIP de Cyber + PDF de Testing + 3–4 repositórios/links. Gravar cada vídeo logo depois de terminar a parte.
