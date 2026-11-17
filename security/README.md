# Segurança de dados pessoais · Cybersecurity

**Integrante:** Luiz Henrique Dos Reis Grabe · RM 556001

O sistema guarda CPF, RG, endereço, condição de saúde e senha de moradores idosos. Este módulo impede que esses dados
saiam por log, resposta da API, pedido à IA ou arquivo, e mostra o resultado no painel Risco Cidadão.

## Os quatro itens

| Item | Implementação | Arquivos |
|---|---|---|
| Parsing de PII | Regex para CPF (com dígito verificador), RG, senha, condição de saúde, e-mail, telefone, endereço e chaves; mascaramento antes do log | `api/src/Termometro.Domain/Seguranca/DetectorDePii.cs`, `PiiSanitizer.cs`, `PoliticaDeSaida.cs` |
| Taint analysis | Semgrep em modo taint com regras geradas a partir dos campos `[Pii]`; guardas de log e de resposta em execução | `security/regras/`, `security/ferramentas/`, `api/src/Termometro.Api/Seguranca/` |
| IA | A Mistral transforma o resumo dos achados (só números e tipos de dado) em um alerta em linguagem simples; sem IA, usa um texto pronto | `api/src/Termometro.Api/Seguranca/TradutorMistral.cs`, `api/src/Termometro.Application/Seguranca/` |
| Dashboard | Nota de 0 a 100 com classificação verde, amarelo ou vermelho e o alerta | `api/src/Termometro.Api/wwwroot/painel/index.html` |

## Auditoria

```bash
pip install semgrep
bash security/ferramentas/rodar_auditoria.sh            # gera as regras e roda as quatro etapas
bash security/ferramentas/rodar_auditoria.sh --enviar   # idem, enviando os achados ao painel
```

| Alvo | Resultado |
|---|---|
| Código vulnerável de propósito (`auditoria/prototipo-vulneravel/`, 12 falhas) | todas acusadas |
| Código correto de controle | nenhum achado |
| Código real da API (`api/src`) | nenhum fluxo de dado pessoal sem sanitizar |
| Segredos no repositório (Trufflehog) | nenhum |

Prints em `auditoria/evidencias/`.

## Painel

Com a API rodando (`dotnet run --project api/src/Termometro.Api`), abra `http://localhost:5080/painel/index.html`
e entre com `admin@demo.local` / `Demo@2026!`. O botão "Simular vazamento" mostra os guardas mascarando os dados.

Para o alerta escrito pela IA, defina a chave antes de rodar a API: `export MISTRAL_API_KEY=...` (nunca no repositório).

## Limitações

- O Semgrep não reconhece construtores primários do C# 12 em alguns arquivos; o corpo dos métodos continua sendo analisado.
- A análise estática procura padrões; por isso existem também os guardas em execução.
