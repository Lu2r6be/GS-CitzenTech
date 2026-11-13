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


def sim(ano=2023):
    """Óbitos do SIM (DATASUS) para SP, via PySUS 0.9.x.

    Códigos úteis: CODMUNRES 355030 = São Paulo (capital);
    IDADE vem codificada: 1º dígito = unidade (4 = anos, 5 = 100+ anos).
    """
    print(f"\n== SIM {ano} (óbitos, SP) via PySUS")
    try:
        from pysus.online_data.SIM import download
        from pysus.online_data import parquets_to_dataframe

        caminho = download("SP", ano)  # baixa do FTP do DATASUS; pode demorar
        if isinstance(caminho, list):
            caminho = caminho[0]
        df = parquets_to_dataframe(caminho)
        print("Colunas:", list(df.columns))
        for c in ("CODMUNRES", "DTOBITO", "IDADE", "CAUSABAS", "CODBAIRES"):
            print(c, "presente" if c in df.columns else "AUSENTE")
        print(df[["CODMUNRES", "DTOBITO", "IDADE"]].head())
        print("Óbitos no estado de SP:", len(df))

        capital = df[df["CODMUNRES"].astype("string").str.startswith("355030")]
        idade = capital["IDADE"].astype("string")
        anos = pd_to_num(idade.str[1:])
        e_60_mais = ((idade.str[0] == "4") & (anos >= 60)) | (idade.str[0] == "5")
        idosos = capital[e_60_mais.fillna(False)]
        datas = idosos["DTOBITO"].astype("string").str.zfill(8)
        por_dia = datas.value_counts()
        print(f"Capital: {len(capital)} óbitos; 60+: {len(idosos)}")
        print("Óbitos 60+ por dia (capital): média %.1f, mín %d, máx %d, dias %d"
              % (por_dia.mean(), por_dia.min(), por_dia.max(), len(por_dia)))
        print("Exemplo de DTOBITO:", datas.iloc[0], "(esperado DDMMAAAA)")
    except Exception as e:  # noqa: BLE001
        print("Falhou:", repr(e))
        print("Alternativa: baixe o CSV do SIM no portal OpenDataSUS "
              "(opendatasus.saude.gov.br, dataset SIM) e rode df.columns.")


def pd_to_num(serie):
    import pandas as pd
    return pd.to_numeric(serie, errors="coerce")


if __name__ == "__main__":
    import os
    os.makedirs("dados/brutos", exist_ok=True)
    # "python3 checar_dados.py sim" pula o INMET (já baixado)
    if "sim" not in sys.argv[1:]:
        try:
            inmet()
        except Exception as e:  # noqa: BLE001
            print("INMET falhou:", repr(e), file=sys.stderr)
    sim()
