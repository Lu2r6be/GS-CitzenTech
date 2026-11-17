"""Simulador das casas da demonstração (contrato §10b).

A casa da avó tem o ESP32 de verdade (Wokwi). As OUTRAS 4 casas são simuladas aqui:
este script faz o papel dos sensores delas e manda leituras para a API.

ATENÇÃO: estes dados são FICTÍCIOS, só para a demo do app. Nunca entram no ML
(a rubrica de IA exige dados reais). Declarar isso no vídeo.

Só usa a biblioteca padrão do Python: não precisa instalar nada.
"""

from __future__ import annotations

import argparse
import json
import os
import random
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass

CABECALHO_DA_CHAVE = "X-Device-Key"
CENARIOS = ("estavel", "onda-de-calor", "onda-de-frio")


@dataclass(frozen=True)
class CasaSimulada:
    device_id: str
    descricao: str
    temperatura_base: float
    umidade_base: float


# Valores escolhidos para a fila ter um exemplo de cada cor (com a regra inicial de limiares):
# vermelho, amarelo, verde e azul. Os device_id são os do cenário de demonstração da API.
CASAS = (
    CasaSimulada("sim-002", "José: calor perigoso", 40.0, 50.0),
    CasaSimulada("sim-003", "Antônia: calor, atenção", 33.0, 50.0),
    CasaSimulada("sim-004", "Benedito: confortável", 25.0, 50.0),
    CasaSimulada("sim-005", "Célia: frio", 15.0, 50.0),
)

# Quanto a temperatura muda por rodada em cada cenário, e até onde (para não ficar absurdo).
VARIACAO_POR_RODADA = {"estavel": 0.0, "onda-de-calor": 0.6, "onda-de-frio": -0.6}
LIMITE_DA_VARIACAO = 8.0
RUIDO_MAXIMO = 0.3  # um sensor de verdade nunca marca exatamente o mesmo valor


def leitura_da_rodada(casa: CasaSimulada, cenario: str, rodada: int,
                      aleatorio: random.Random) -> tuple[float, float]:
    """Temperatura e umidade que o 'sensor' da casa mede nesta rodada (função pura, testável)."""
    if cenario not in VARIACAO_POR_RODADA:
        raise ValueError(f"Cenário desconhecido: {cenario!r}. Use um de {CENARIOS}.")

    passo = VARIACAO_POR_RODADA[cenario]
    deslocamento = max(-LIMITE_DA_VARIACAO, min(LIMITE_DA_VARIACAO, passo * rodada))
    temperatura = casa.temperatura_base + deslocamento + aleatorio.uniform(-RUIDO_MAXIMO, RUIDO_MAXIMO)
    umidade = casa.umidade_base + aleatorio.uniform(-1.0, 1.0)
    return round(temperatura, 1), round(min(100.0, max(0.0, umidade)), 1)


def montar_corpo(device_id: str, temperatura: float, umidade: float) -> bytes:
    """Mesmo JSON do contrato §4 que o ESP32 envia."""
    return json.dumps(
        {"deviceId": device_id, "temperaturaC": temperatura, "umidade": umidade}
    ).encode("utf-8")


def enviar(url_base: str, chave: str, device_id: str, temperatura: float,
           umidade: float, timeout: float = 8.0) -> tuple[int | None, str]:
    """Envia uma leitura. Devolve (status HTTP, nível ou mensagem de erro). Nunca derruba o simulador."""
    pedido = urllib.request.Request(
        url_base.rstrip("/") + "/api/v1/telemetria",
        data=montar_corpo(device_id, temperatura, umidade),
        headers={"Content-Type": "application/json", CABECALHO_DA_CHAVE: chave},
        method="POST",
    )
    try:
        with urllib.request.urlopen(pedido, timeout=timeout) as resposta:
            corpo = json.loads(resposta.read().decode("utf-8") or "{}")
            return resposta.status, str(corpo.get("nivel", "?"))
    except urllib.error.HTTPError as erro:
        return erro.code, f"erro HTTP {erro.code}"
    except (urllib.error.URLError, TimeoutError, OSError) as erro:
        return None, f"sem conexão ({erro.__class__.__name__})"


def executar(url_base: str, chave: str, cenario: str, intervalo: float, rodadas: int,
             semente: int | None = None, dormir=time.sleep) -> None:
    aleatorio = random.Random(semente)
    print(f"Simulando {len(CASAS)} casas | cenário: {cenario} | API: {url_base}")
    print("(dados fictícios, só para a demonstração) — Ctrl+C para parar\n")

    rodada = 0
    while rodadas == 0 or rodada < rodadas:
        for casa in CASAS:
            temperatura, umidade = leitura_da_rodada(casa, cenario, rodada, aleatorio)
            status, resultado = enviar(url_base, chave, casa.device_id, temperatura, umidade)
            marca = "ok" if status == 202 else "FALHA"
            print(f"rodada {rodada + 1:>3} | {casa.device_id} | {temperatura:5.1f} °C | "
                  f"{umidade:4.1f} % -> {resultado:<10} [{marca}]")
        rodada += 1
        if rodadas == 0 or rodada < rodadas:
            dormir(intervalo)


def ler_argumentos(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Simula os sensores das casas da demonstração.")
    parser.add_argument("--url", default=os.environ.get("API_URL", "http://localhost:5080"),
                        help="endereço da API (padrão: http://localhost:5080)")
    parser.add_argument("--cenario", choices=CENARIOS, default="estavel")
    parser.add_argument("--intervalo", type=float, default=10.0, help="segundos entre rodadas")
    parser.add_argument("--rodadas", type=int, default=0, help="quantas rodadas (0 = até Ctrl+C)")
    parser.add_argument("--semente", type=int, default=None, help="fixa o ruído (para repetir igual)")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    argumentos = ler_argumentos(argv)
    # A chave vem de variável de ambiente (como manda o contrato): nunca fica no código nem no repositório.
    chave = os.environ.get("DEVICE_KEY")
    if not chave:
        print("Defina a variável DEVICE_KEY (a chave dos dispositivos da demonstração).", file=sys.stderr)
        return 2
    try:
        executar(argumentos.url, chave, argumentos.cenario, argumentos.intervalo,
                 argumentos.rodadas, argumentos.semente)
    except KeyboardInterrupt:
        print("\nSimulador parado. (Sem leituras por 30 min, a API marca as casas como 'sem sinal'.)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
