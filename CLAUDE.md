# Instruções para o Claude Code

1. **Antes de qualquer tarefa, leia `docs/CONTRATO.md` e siga-o.** Ele define entidades, endpoints, o JSON do ESP32, as regras do semáforo e as regras de PII.
2. Se discordar do contrato ou faltar informação, **pergunte**. Não invente formato de dados, nome de campo ou endpoint.
3. Rubricas de cada disciplina: `docs/rubricas.md`. O código precisa mostrar o que elas pedem.
4. Um pedido = uma tarefa. Mexa só na pasta da tarefa, salvo pedido explícito.
5. Convenções (contrato, seção 8): nomes de domínio em português; regras de negócio só em `Domain`; funções pequenas; async/await sem `.Result`; todo código de regra tem teste; comentários explicam o porquê.
6. **PII:** nunca coloque CPF, nome, endereço, saúde ou senha em logs ou respostas sem mascarar. Segredos só em variáveis de ambiente (`.env`, nunca versionado).
7. **Dados simulados nunca entram em `/ml`** (a rubrica de ML proíbe).
8. Ao terminar: explique em 5 linhas o que fez, por que, e dê uma analogia. Liste o que o Fenyknit deve conferir. Sugira 3 linhas para `docs/GLOSSARIO.md`.
