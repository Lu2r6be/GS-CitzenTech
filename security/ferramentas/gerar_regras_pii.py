#!/usr/bin/env python3
"""
Gera as regras de taint analysis do Semgrep (security/regras/pii-taint.yml) a partir do próprio código.

Por que gerar: a lista de campos com dado pessoal já existe no código, marcada com [Pii(CategoriaPii.X)]
nas entidades do Domain. Ler essas marcas evita manter duas listas que se desencontram. Se alguém
cria um campo novo com [Pii], basta rodar este script de novo.

Como o Semgrep enxerga o fluxo (taint analysis):
  FONTE (source)   de onde o dado pessoal nasce: um membro marcado com [Pii] (ex.: residente.Cpf)
  SUMIDOURO (sink) uma saída insegura: log, resposta HTTP, prompt de LLM, arquivo
  SANITIZADOR      o que "limpa" o dado no caminho: PiiSanitizer.Sanitizar(...), .Mascarado
Se existe um caminho da fonte ao sumidouro SEM passar por um sanitizador, o Semgrep acusa.

Há uma regra por (categoria de dado x saída), então cada achado já nasce com a categoria e a saída certas,
e a política de saída é a mesma do código C# (Termometro.Domain/Seguranca/PoliticaDeSaida.cs):
na resposta HTTP só CPF, RG e senha são proibidos.

Uso:  python3 security/ferramentas/gerar_regras_pii.py [raiz-do-repositorio]
"""
import re
import sys
from pathlib import Path

RAIZ = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
SAIDA = RAIZ / "security" / "regras" / "pii-taint.yml"

# ---- Política de saída (espelha PoliticaDeSaida.cs) ----
CATEGORIAS = ["Nome", "Cpf", "Documento", "Saude", "Endereco", "Contato", "Email", "Senha", "Segredo"]
PROIBIDAS_NA_RESPOSTA = {"Cpf", "Senha", "Documento"}

# ---- Severidade padrão (espelha CalculadorDoRiscoCidadao.SeveridadePadrao) ----
ORDEM = ["baixa", "media", "alta", "critica"]


def severidade(categoria: str, saida: str) -> str:
    base = {"Senha": "critica", "Segredo": "critica", "Saude": "alta", "Cpf": "alta", "Documento": "alta",
            "Endereco": "media", "Contato": "media", "Email": "media"}.get(categoria, "baixa")
    if saida == "PromptLlm" and base != "critica":
        base = ORDEM[ORDEM.index(base) + 1]
    return base


# ---- Sumidouros por saída ----
SUMIDOUROS = {
    "Log": {
        "rotulo": "log",
        "cwe": "CWE-532",
        "padroes": [
            "$L.LogTrace(...)", "$L.LogDebug(...)", "$L.LogInformation(...)", "$L.LogWarning(...)",
            "$L.LogError(...)", "$L.LogCritical(...)", "$L.Log(...)",
            "Console.WriteLine(...)", "Console.Write(...)", "Console.Error.WriteLine(...)",
            "Debug.WriteLine(...)", "Trace.WriteLine(...)",
        ],
    },
    "RespostaHttp": {
        "rotulo": "resposta-http",
        "cwe": "CWE-200",
        "padroes": [
            "Ok(...)", "Created(...)", "CreatedAtAction(...)", "Accepted(...)", "BadRequest(...)",
            "Conflict(...)", "NotFound(...)", "Problem(...)", "StatusCode(...)", "Content(...)", "Json(...)",
            "Results.Ok(...)", "Results.Json(...)", "$R.WriteAsJsonAsync(...)", "$R.WriteAsync(...)",
            "new ProblemDetails { ... }",
            # Mensagem de erro de aplicação vira o "detalhe" da resposta (TradutorDeErros usa e.Message).
            "new DadosInvalidos(...)", "new NaoAutenticado(...)", "new AcessoNegado(...)",
            "new NaoEncontrado(...)", "new Conflito(...)",
        ],
    },
    "PromptLlm": {
        "rotulo": "llm",
        "cwe": "CWE-359",
        "padroes": [
            "$C.TraduzirAsync(...)", "$C.CompletarAsync(...)", "$C.PerguntarAsync(...)",
            "$H.PostAsJsonAsync(...)", "$H.PostAsync(...)", "new StringContent(...)", "JsonContent.Create(...)",
        ],
    },
    "Arquivo": {
        "rotulo": "arquivo",
        "cwe": "CWE-312",
        "padroes": [
            "File.WriteAllText(...)", "File.WriteAllTextAsync(...)", "File.AppendAllText(...)",
            "File.AppendAllTextAsync(...)", "File.WriteAllLines(...)", "File.WriteAllBytes(...)",
        ],
    },
}

# ---- Sanitizadores: o que limpa o dado no caminho ----
SANITIZADORES = [
    "$S.Sanitizar(...)",   # PiiSanitizer.Sanitizar
    "$X.Mascarado",        # Cpf.Mascarado
    "Mascarar(...)",
]

# Apelidos: nomes que carregam o mesmo tipo de dado mas não têm a marca [Pii] (pedidos, configurações).
APELIDOS = {
    "Documento": ["Rg", "Documento", "NumeroDoRg"],
    "Senha": ["Senha", "Password", "SenhaDosUsuarios"],
    "Segredo": ["ApiKey", "Chave", "ChaveDoDispositivo", "DeviceKey"],
}
# Nomes comuns demais para virar variável solta (muita falsa-positiva): só o acesso por membro conta.
SO_COMO_MEMBRO = {"Nome", "Observacao", "Chave"}

PADRAO_PII = re.compile(r"\[Pii\(CategoriaPii\.(\w+)\)\]\s*public\s+[\w<>?\[\], ]+?\s+(\w+)\s*\{")


def membros_pii():
    por_categoria = {c: set() for c in CATEGORIAS}
    for arquivo in sorted((RAIZ / "api" / "src").rglob("*.cs")):
        if "/obj/" in str(arquivo) or "/bin/" in str(arquivo):
            continue
        for m in PADRAO_PII.finditer(arquivo.read_text(encoding="utf-8")):
            categoria, nome = m.group(1), m.group(2)
            if categoria in por_categoria:
                por_categoria[categoria].add(nome)
    for categoria, nomes in APELIDOS.items():
        por_categoria[categoria].update(nomes)
    return por_categoria


def lower_camel(nome: str) -> str:
    return nome[0].lower() + nome[1:]


def emitir_regra(categoria: str, saida: str, nomes: set) -> str:
    info = SUMIDOUROS[saida]
    nomes_ordenados = sorted(nomes)
    fontes = [f"    - pattern: $X.{n}" for n in nomes_ordenados]
    fontes += [f"    - pattern: {lower_camel(n)}" for n in nomes_ordenados if n not in SO_COMO_MEMBRO]
    sinks = [f"    - pattern: {p}" for p in info["padroes"]]
    sans = [f"    - pattern: {p}" for p in SANITIZADORES]
    dado = {"Cpf": "CPF", "Documento": "RG (documento de identidade)", "Saude": "informação de saúde", "Senha": "senha", "Segredo": "chave ou token",
            "Endereco": "endereço", "Contato": "telefone", "Email": "e-mail", "Nome": "nome"}[categoria]
    destino = {"Log": "um log", "RespostaHttp": "uma resposta HTTP", "PromptLlm": "o prompt ou a requisição de um serviço externo (LLM)",
               "Arquivo": "um arquivo"}[saida]
    return f"""  - id: pii-{categoria.lower()}-para-{info['rotulo']}
    mode: taint
    languages: [csharp]
    severity: ERROR
    message: >-
      {dado[0].upper() + dado[1:]} pode chegar a {destino} sem passar pelo PiiSanitizer.
      Troque por um identificador, use .Mascarado ou chame PiiSanitizer.Sanitizar antes.
    metadata:
      categoria: {categoria}
      exposicao: {saida}
      severidade: {severidade(categoria, saida)}
      cwe: {info['cwe']}
      owasp: "API3:2023 Broken Object Property Level Authorization / A09:2021 Security Logging and Monitoring Failures"
    pattern-sources:
{chr(10).join(fontes)}
    pattern-sanitizers:
{chr(10).join(sans)}
    pattern-sinks:
{chr(10).join(sinks)}
"""


def main() -> int:
    membros = membros_pii()
    regras = []
    for saida in SUMIDOUROS:
        proibidas = PROIBIDAS_NA_RESPOSTA if saida == "RespostaHttp" else set(CATEGORIAS)
        for categoria in CATEGORIAS:
            if categoria in proibidas and membros[categoria]:
                regras.append(emitir_regra(categoria, saida, membros[categoria]))

    cabecalho = (
        "# ARQUIVO GERADO por security/ferramentas/gerar_regras_pii.py. NÃO edite à mão: rode o script de novo.\n"
        "# Fontes: membros com [Pii(CategoriaPii.X)] em api/src. Veja security/regras/README.md.\n"
        "rules:\n"
    )
    SAIDA.parent.mkdir(parents=True, exist_ok=True)
    SAIDA.write_text(cabecalho + "\n".join(regras), encoding="utf-8")

    print(f"{len(regras)} regras escritas em {SAIDA.relative_to(RAIZ)}")
    for c in CATEGORIAS:
        print(f"  {c:9s} fontes: {', '.join(sorted(membros[c])) or '-'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
