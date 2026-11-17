# Protótipo vulnerável (de propósito)

Código escrito com falhas de propósito para provar que o Semgrep encontra o problema. Não é compilado
(não faz parte do `Termometro.sln`).

- `CadastroInseguro.cs`, `LoginInseguro.cs`, `ExportacaoInsegura.cs`: cada linha marcada com `// VULNERAVEL` deve ser acusada.
- `ControleSeguro.cs`: o mesmo trabalho feito do jeito certo; não deve ser acusado.
