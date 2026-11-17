#!/usr/bin/env python3
"""
Leva o resultado do Semgrep (e do Trufflehog) para o painel Risco Cidadão da API.

Fluxo:  semgrep --json  ->  este script  ->  POST /api/v1/seguranca/achados  ->  painel

Cuidados de privacidade:
  * O script NUNCA envia o trecho de código nem o valor encontrado. Só: regra, severidade, arquivo:linha,
    saída (log, resposta...) e categoria. Um relatório de vazamento que vaza não serve.
  * A senha do administrador vem de variável de ambiente, nunca de argumento (argumentos aparecem em `ps`).
  * A API ainda confere tudo de novo: rejeita 'tipo' ou 'origem' com cara de dado pessoal.

Exemplos:
  python3 security/ferramentas/importar_achados.py --semgrep semgrep.json --dry-run
  TERMOMETRO_SENHA='...' python3 security/ferramentas/importar_achados.py \\
      --semgrep semgrep.json --api http://localhost:5080 --email admin@demo.local

Cada execução SUBSTITUI os achados anteriores da mesma ferramenta (substituirOrigem): corrigiu o código,
rodou de novo, o achado some e o indicador cai. Varredura limpa também é enviada (lista vazia).
"""
import argparse
import json
import os
import sys
import urllib.error
import urllib.request

SEVERIDADES = {"baixa", "media", "alta", "critica"}
EXPOSICOES = {"log": "Log", "respostahttp": "RespostaHttp", "promptllm": "PromptLlm", "arquivo": "Arquivo"}
MAX_LOTE = 500  # igual ao limite da API


def caminho_curto(caminho: str) -> str:
    """Tira prefixos de diretório da máquina; fica algo como api/src/Termometro.Api/X.cs."""
    caminho = caminho.replace("\\", "/")
    for marca in ("api/src/", "security/"):
        if marca in caminho:
            return caminho[caminho.index(marca):]
    return caminho.lstrip("./")


def de_semgrep(arquivo: str) -> list[dict]:
    with open(arquivo, encoding="utf-8") as f:
        dados = json.load(f)
    achados, vistos = [], set()
    for r in dados.get("results", []):
        meta = r.get("extra", {}).get("metadata", {})
        regra = r["check_id"].split(".")[-1]
        severidade = str(meta.get("severidade", "alta")).lower()
        exposicao = str(meta.get("exposicao", "")).lower()
        if severidade not in SEVERIDADES or exposicao not in EXPOSICOES:
            print(f"AVISO: regra {regra} sem metadata válida (severidade/exposicao); achado ignorado.", file=sys.stderr)
            continue
        origem = f"semgrep:{regra}@{caminho_curto(r['path'])}:{r['start']['line']}"[:200]
        if origem in vistos:  # mesmo fluxo reportado duas vezes
            continue
        vistos.add(origem)
        achados.append({
            "tipo": f"Fluxo de dado pessoal sem sanitizar ({regra})"[:100],
            "severidade": severidade,
            "origem": origem,
            "exposicao": EXPOSICOES[exposicao],
            "categoriaPii": meta.get("categoria"),
        })
    return achados


def de_trufflehog(arquivo: str) -> list[dict]:
    """Trufflehog escreve um JSON por linha. O campo 'Raw' (o segredo em si) é descartado de propósito."""
    achados, vistos = [], set()
    with open(arquivo, encoding="utf-8") as f:
        for linha in f:
            linha = linha.strip()
            if not linha.startswith("{"):
                continue
            r = json.loads(linha)
            if "DetectorName" not in r:
                continue
            fs = (r.get("SourceMetadata", {}).get("Data", {}) or {})
            local = next(iter(fs.values()), {}) if fs else {}
            arq = caminho_curto(str(local.get("file", "desconhecido")))
            origem = f"trufflehog:{r['DetectorName']}@{arq}:{local.get('line', 0)}"[:200]
            if origem in vistos:
                continue
            vistos.add(origem)
            achados.append({
                "tipo": f"Segredo no repositório ({r['DetectorName']})"[:100],
                "severidade": "critica" if r.get("Verified") else "alta",
                "origem": origem,
                "exposicao": "Arquivo",
                "categoriaPii": "Segredo",
            })
    return achados


def enviar(api: str, email: str, senha: str, pedido: dict) -> dict:
    def chamar(caminho, corpo, token=None):
        req = urllib.request.Request(
            api.rstrip("/") + caminho, data=json.dumps(corpo).encode(), method="POST",
            headers={"Content-Type": "application/json", **({"Authorization": f"Bearer {token}"} if token else {})})
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                return json.load(resp)
        except urllib.error.HTTPError as e:
            raise SystemExit(f"ERRO: a API respondeu {e.code} em {caminho}: {e.read().decode(errors='replace')[:300]}")
        except urllib.error.URLError as e:
            raise SystemExit(f"ERRO: não consegui falar com {api}: {e.reason}")

    token = chamar("/api/v1/auth/login", {"email": email, "senha": senha})["token"]
    return chamar("/api/v1/seguranca/achados", pedido, token)


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--semgrep", help="JSON do semgrep (--json)")
    p.add_argument("--trufflehog", help="JSON Lines do trufflehog (--json)")
    p.add_argument("--api", default=os.environ.get("TERMOMETRO_API", "http://localhost:5080"))
    p.add_argument("--email", default=os.environ.get("TERMOMETRO_EMAIL", "admin@demo.local"))
    p.add_argument("--dry-run", action="store_true", help="mostra o que enviaria, sem enviar")
    a = p.parse_args()
    if not a.semgrep and not a.trufflehog:
        p.error("informe --semgrep e/ou --trufflehog")

    ferramentas = []
    if a.semgrep:
        ferramentas.append(("semgrep:", de_semgrep(a.semgrep)))
    if a.trufflehog:
        ferramentas.append(("trufflehog:", de_trufflehog(a.trufflehog)))

    senha = os.environ.get("TERMOMETRO_SENHA", "")
    if not a.dry_run and not senha:
        raise SystemExit("ERRO: defina a variável TERMOMETRO_SENHA (senha do administrador). Ela não é aceita por argumento.")

    for prefixo, achados in ferramentas:
        if len(achados) > MAX_LOTE:
            print(f"AVISO: {len(achados)} achados de {prefixo} excedem o lote; enviando só os {MAX_LOTE} primeiros.", file=sys.stderr)
            achados = achados[:MAX_LOTE]
        pedido = {"achados": achados, "substituirOrigem": prefixo}
        por_sev = {}
        for x in achados:
            por_sev[x["severidade"]] = por_sev.get(x["severidade"], 0) + 1
        print(f"{prefixo:12s} {len(achados)} achado(s) {por_sev or ''}")
        if a.dry_run:
            for x in achados:
                print(f"   [{x['severidade']:7s}] {x['categoriaPii'] or '-':8s} -> {x['exposicao']:12s} {x['origem']}")
            continue
        r = enviar(a.api, a.email, senha, pedido)
        print(f"   enviado: {r['gravados']} gravado(s), {r['substituidos']} da varredura anterior substituído(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
