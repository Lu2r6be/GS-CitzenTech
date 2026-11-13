"""Baixa e prepara os dados REAIS do ML (passo 1).

Saídas (pequenas, versionadas em dados/processados/):
  clima_a701_diario.csv   -> clima diário da estação INMET A701 (Mirante de Santana, SP)
  obitos60_capital_diario.csv -> óbitos diários de pessoas 60+ residentes na capital (SIM)

Uso:
  python3 baixar_dados.py            # INMET + SIM, anos padrão
  python3 baixar_dados.py inmet      # só INMET
  python3 baixar_dados.py sim        # só SIM

Por que diário: o SIM registra a DATA do óbito, não a hora. Então o clima também
vira um valor por dia (máxima, mínima, média), no horário de Brasília.
"""
import io
import sys
import time
import unicodedata
import urllib.request
import zipfile
from pathlib import Path

import pandas as pd

ANOS = list(range(2014, 2024))  # 2014–2023: SIM com dados finais; 10 anos ≈ 3.650 dias
ESTACAO = "A701"
COD_CAPITAL = "355030"  # São Paulo (IBGE, 6 dígitos usados pelo SIM)
PASTA = Path(__file__).parent / "dados"
BRUTOS = PASTA / "brutos"
PROC = PASTA / "processados"


# ---------------------------------------------------------------- INMET
def _sem_acento(txt):
    return unicodedata.normalize("NFKD", txt).encode("ascii", "ignore").decode().upper()


def _achar_coluna(colunas, *palavras):
    """Os nomes das colunas mudam um pouco entre anos; achamos por palavra-chave."""
    for c in colunas:
        if all(p in _sem_acento(c) for p in palavras):
            return c
    raise KeyError(f"coluna com {palavras} não encontrada")


def _baixar_com_retentativa(url, tentativas=4):
    """O servidor do INMET às vezes corta o download no meio; tentamos de novo."""
    for n in range(1, tentativas + 1):
        try:
            print(f"  baixando {url} (tentativa {n}/{tentativas}, demora)")
            req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
            return urllib.request.urlopen(req, timeout=300).read()
        except Exception as e:  # noqa: BLE001
            print(f"  falhou: {e!r}")
            if n == tentativas:
                raise
            time.sleep(10 * n)


def _csv_a701_do_ano(ano):
    destino = BRUTOS / "inmet" / f"{ESTACAO}_{ano}.csv"
    if destino.exists():
        return destino
    url = f"https://portal.inmet.gov.br/uploads/dadoshistoricos/{ano}.zip"
    z = zipfile.ZipFile(io.BytesIO(_baixar_com_retentativa(url)))
    nome = next(n for n in z.namelist() if f"_{ESTACAO}_" in n.upper())
    destino.parent.mkdir(parents=True, exist_ok=True)
    destino.write_bytes(z.read(nome))  # guarda só a estação, não o ZIP inteiro
    return destino


def ler_inmet_horario(caminho):
    df = pd.read_csv(caminho, sep=";", skiprows=8, encoding="latin-1",
                     decimal=",", na_values=["-9999", "-9999,0"], dtype=str)
    cols = list(df.columns)
    data = df[cols[0]].str.replace("/", "-", regex=False)
    hora = df[cols[1]].str.replace(r"\D", "", regex=True).str[:2].astype(int)
    utc = pd.to_datetime(data, format="%Y-%m-%d") + pd.to_timedelta(hora, unit="h")
    num = lambda c: pd.to_numeric(df[c].str.replace(",", ".", regex=False), errors="coerce")
    return pd.DataFrame({
        # Brasil sem horário de verão desde 2019; usamos UTC-3 fixo em todos os anos
        "data": (utc - pd.Timedelta(hours=3)).dt.normalize(),
        "temp": num(_achar_coluna(cols, "BULBO SECO")),
        "tmax_h": num(_achar_coluna(cols, "TEMPERATURA MAXIMA")),
        "tmin_h": num(_achar_coluna(cols, "TEMPERATURA MINIMA")),
        "umid": num(_achar_coluna(cols, "UMIDADE RELATIVA DO AR")),
        "orvalho": num(_achar_coluna(cols, "PONTO DE ORVALHO")),
        "chuva": num(_achar_coluna(cols, "PRECIPITACAO")),
    })


def inmet_diario(horario):
    g = horario.groupby("data")
    dia = pd.DataFrame({
        "tmax": g["tmax_h"].max(),
        "tmin": g["tmin_h"].min(),
        "tmed": g["temp"].mean(),
        "umid_med": g["umid"].mean(),
        "umid_min": g["umid"].min(),
        "orvalho_med": g["orvalho"].mean(),
        "chuva_mm": g["chuva"].sum(min_count=1),
        "horas_validas": g["temp"].count(),
    })
    # Dia com muitas horas faltando dá máxima/mínima enganosa: vira vazio (tratado no notebook)
    ruins = dia["horas_validas"] < 18
    dia.loc[ruins, ["tmax", "tmin", "tmed", "umid_med", "umid_min", "orvalho_med"]] = float("nan")
    return dia.round(2)


def rodar_inmet(anos=ANOS):
    horas = []
    for ano in anos:
        print(f"INMET {ano}")
        horas.append(ler_inmet_horario(_csv_a701_do_ano(ano)))
    # Junta as HORAS antes de agrupar: em UTC-3, o 31/12 fica dividido entre dois arquivos
    clima = inmet_diario(pd.concat(horas, ignore_index=True))
    PROC.mkdir(parents=True, exist_ok=True)
    clima.to_csv(PROC / "clima_a701_diario.csv", index_label="data")
    print(f"  -> {len(clima)} dias; {int(clima['tmax'].isna().sum())} sem máxima válida")


# ---------------------------------------------------------------- SIM
def obitos60_do_ano(ano):
    from pysus.online_data import parquets_to_dataframe
    from pysus.online_data.SIM import download

    caminho = download("SP", ano)
    caminho = caminho[0] if isinstance(caminho, list) else caminho
    df = parquets_to_dataframe(caminho)[["CODMUNRES", "DTOBITO", "IDADE", "CAUSABAS"]]
    df = df[df["CODMUNRES"].astype("string").str.startswith(COD_CAPITAL)]
    idade = df["IDADE"].astype("string")
    # IDADE: 1º dígito é a unidade (4 = anos, 5 = mais de 100 anos)
    anos = pd.to_numeric(idade.str[1:], errors="coerce")
    df = df[(((idade.str[0] == "4") & (anos >= 60)) | (idade.str[0] == "5")).fillna(False)]
    data = pd.to_datetime(df["DTOBITO"].astype("string").str.zfill(8),
                          format="%d%m%Y", errors="coerce")
    causa = df["CAUSABAS"].astype("string").str[0]
    out = pd.DataFrame({"data": data, "circ_resp": causa.isin(["I", "J"]).fillna(False)})
    return out.groupby("data").agg(obitos_60mais=("circ_resp", "size"),
                                   obitos_60mais_circ_resp=("circ_resp", "sum"))


def rodar_sim(anos=ANOS):
    partes = []
    for ano in anos:
        print(f"SIM {ano}")
        partes.append(obitos60_do_ano(ano))
    ob = pd.concat(partes).sort_index()
    PROC.mkdir(parents=True, exist_ok=True)
    ob.to_csv(PROC / "obitos60_capital_diario.csv", index_label="data")
    print(f"  -> {len(ob)} dias; média {ob['obitos_60mais'].mean():.1f} óbitos 60+/dia")


if __name__ == "__main__":
    alvo = sys.argv[1] if len(sys.argv) > 1 else "tudo"
    if alvo in ("tudo", "inmet"):
        rodar_inmet()
    if alvo in ("tudo", "sim"):
        rodar_sim()
