# ML — Calor, frio e óbitos de idosos em São Paulo

**Vídeo:** https://youtu.be/iGxmVT65Vp4

## A pergunta
**A partir de que temperatura lá fora morrem mais idosos (60+) em São Paulo?**
A resposta vira a régua do app: nesses dias, o agente de saúde é avisado de que o dia é perigoso.

## O que fizemos
1. Juntamos 10 anos (2014–2023) de **clima** (INMET, estação Mirante de Santana) e **óbitos de 60+** na capital (SIM/DATASUS). Só dado real e público.
2. Tiramos do treino **2020–2021 e jan–fev/2022** (COVID: excesso de até +49% de mortes, que não tem a ver com clima).
3. Transformamos "número de óbitos" em **risco relativo**: quanto o dia está acima ou abaixo do normal (1,10 = 10% acima). Assim o envelhecimento da população não atrapalha.
4. Treinamos uma **Random Forest** (mesmo padrão da aula de 19/08: `GridSearchCV`) com 24 combinações de hiperparâmetros, treino em 2014–2022 e **teste em 2023** (um ano que o modelo nunca viu).

## O que vimos
- **A curva tem forma de "U":** o risco sobe no frio e no calor. O dia mais seguro tem máxima perto de **31 °C**.
- **Dia perigoso de calor:** máxima **≥ 36 °C** (+7% de óbitos). Poucos dias assim, então é um indício.
- **Dia perigoso de frio:** mínima **≤ 13 °C** (+6%). Acontece em muito mais dias: em SP, o frio pesa mais que o calor.
- **Noites quentes** (mínima ≥ 23 °C) tiveram +12% de óbitos.
- No teste, o modelo erra ~13 óbitos/dia; chutar "todo dia é normal" erra ~17. O erro está perto do mínimo possível: óbitos diários variam por acaso em torno de ±13.
- **Overfitting:** árvores sem limite de profundidade erraram 0,03 no treino e 0,08 na validação (decoraram). Venceu profundidade 5: erros quase iguais nos dois.

## O que entendemos
O clima de fora explica **parte** das mortes de idosos, e o frio importa tanto quanto o calor. Por isso o semáforo do app ganhou o nível **azul (frio)**.
O modelo descreve a **cidade inteira**: ele não diz quem vai adoecer. Quem mede cada casa é o sensor.

## Como ver
- **Notebook (com gráficos):** `modelo_calor_obitos.ipynb` — abra no VS Code.
- **Gráficos e números:** pasta `resultados/` (a curva em U é `07_curva_risco.png`).
- **Refazer do zero:** `pip install -r requirements.txt` → `python3 baixar_dados.py` → rodar o notebook.
- Fontes dos dados: `dados/README.md`.
