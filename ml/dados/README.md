# Dados (reais e públicos)

| Fonte | O que usamos | Onde | Período |
|---|---|---|---|
| INMET (estação automática **A701 – São Paulo/Mirante de Santana**, lat -23,50, lon -46,62) | temperatura (bulbo seco, máx./mín. horárias), umidade relativa, ponto de orvalho, chuva — horário em UTC | portal.inmet.gov.br → Dados Históricos (ZIP anual) | 2014–2023 |
| SIM / DATASUS (Declarações de Óbito, UF SP) | `DTOBITO` (DDMMAAAA), `IDADE` (1º dígito = unidade: 4 = anos, 5 = 100+), `CODMUNRES` (355030 = capital), `CAUSABAS` (CID-10) | FTP do DATASUS via PySUS 0.9.4 | 2014–2023 |

Validação (10/11/2026): SIM 2023 tem 334.303 óbitos no estado; 81.653 de residentes da capital; **60.941 de 60+ (média 167/dia, mín. 126, máx. 242, 365 dias)**.

## Como reproduzir
```bash
pip install -r ../requirements.txt
python3 ../baixar_dados.py        # gera dados/processados/*.csv
```
- `processados/clima_a701_diario.csv`: um dia por linha, horário de Brasília (UTC-3 fixo). Dias com menos de 18 horas válidas ficam sem máxima/mínima.
- `processados/obitos60_capital_diario.csv`: óbitos 60+ residentes na capital por dia; `circ_resp` = causas do aparelho circulatório (CID I) e respiratório (J).
- `brutos/` fica fora do Git (tamanho).
