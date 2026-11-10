"""Passo 0 do ML: confere se os dados do INMET e do SIM abrem e quais colunas têm.

Rode no seu computador: python3 checar_dados.py  (para o SIM: pip install pysus)
Cole a saída na conversa. A URL do INMET vem de memória; se falhar, baixe o ZIP
manualmente em portal.inmet.gov.br (Dados Históricos).
"""
import io
import sys
import urllib.request
import zipfile

ANO = 2025
URL_INMET = f"https://portal.inmet.gov.br/uploads/dadoshistoricos/{ANO}.zip"


def inmet():
    print(f"== INMET {ANO}: baixando {URL_INMET}")
    req = urllib.request.Request(URL_INMET, headers={"User-Agent": "Mozilla/5.0"})
    data = urllib.request.urlopen(req, timeout=180).read()
    z = zipfile.ZipFile(io.BytesIO(data))
    sp = [n for n in z.namelist() if "_SP_" in n.upper()]
    print(f"{len(z.namelist())} arquivos; {len(sp)} de SP. Capital:")
    cap = [n for n in sp if "SAO PAULO" in n.upper() or "MIRANTE" in n.upper()]
    for n in cap:
        print(" -", n)
    if cap:
        raw = z.read(cap[0]).decode("latin-1").splitlines()
        print("\nCabeçalho (8 linhas de metadados):")
        for l in raw[:8]:
            print("  ", l)
        print("\nColunas:", raw[8])
        print("Linhas de dados:", len(raw) - 9)
        print("Exemplo:", raw[9])
    z.extractall(f"dados/brutos/inmet_{ANO}")
    print(f"Extraído em ./dados/brutos/inmet_{ANO}")


def sim():
    print("\n== SIM (óbitos) via PySUS (pip install pysus)")
    try:
        from pysus import SIM  # API pode variar conforme a versão do PySUS

        s = SIM().load()
        files = s.get_files("CID10", uf="SP", year=2023)
        print("Arquivos:", files)
        df = s.download(files)[0].to_dataframe()
        print("Colunas:", list(df.columns))
        for c in ("CODMUNRES", "DTOBITO", "IDADE", "CAUSABAS", "CODBAIRES"):
            print(c, "presente" if c in df.columns else "AUSENTE")
        print(df[["CODMUNRES", "DTOBITO", "IDADE"]].head())
        print("Óbitos em SP em 2023:", len(df))
        df.to_parquet("dados/brutos/sim_sp_2023.parquet")
    except Exception as e:  # noqa: BLE001
        print("Falhou:", repr(e))
        print("Alternativa: baixe o CSV do SIM no portal OpenDataSUS "
              "(opendatasus.saude.gov.br, dataset SIM) e rode df.columns.")


if __name__ == "__main__":
    import os
    os.makedirs("dados/brutos", exist_ok=True)
    try:
        inmet()
    except Exception as e:  # noqa: BLE001
        print("INMET falhou:", repr(e), file=sys.stderr)
    sim()
