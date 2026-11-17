# Regras do Semgrep

`pii-taint.yml` é **gerado** por `../ferramentas/gerar_regras_pii.py`: ele lê os campos marcados com `[Pii(CategoriaPii.X)]`
nas entidades e escreve uma regra por (tipo de dado x saída). Assim a lista de campos sensíveis existe num só lugar (o código).
Criou um campo novo com `[Pii]`? Rode o script de novo (a auditoria já faz isso).

Cada regra tem `metadata` (categoria, saída, severidade, CWE) que o importador usa para montar o achado do painel.
Os sanitizadores que "limpam" o dado no caminho: `PiiSanitizer.Sanitizar(...)`, `.Mascarado`, `Mascarar(...)`.
