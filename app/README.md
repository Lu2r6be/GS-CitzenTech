# App Termômetro do ACS (Expo + TypeScript) · Mobile Development & IoT

**Integrante:** Luiz Henrique Dos Reis Grabe · RM 556001
**Vídeo:** _(link)_

## Ficha da CitizenTech

| | |
|---|---|
| **Nome** | Termômetro do ACS |
| **Cidade** | São Paulo (SP) |
| **Cidadão beneficiado** | Pessoas idosas em casas quentes ou frias demais e os agentes comunitários de saúde (ACS) que as acompanham. |
| **Problema** | O ACS não sabe quais casas de idosos estão perigosas agora: a previsão do tempo mede a rua, não a sala onde a pessoa está. |
| **Como funciona** | Um sensor mede temperatura e umidade dentro da casa, a API classifica por cor e o app mostra ao ACS uma fila de visitas, da casa mais urgente para a menos. O familiar acompanha a casa do parente. |
| **Repositório** | _(link do GitHub)_ |
| **Indicador de impacto** | Hipótese: reduzir o tempo entre a casa ficar perigosa e a visita do ACS. Medida: tempo médio entre a primeira leitura vermelha e a visita. |
| **ODS** | 3 (Saúde e bem-estar), 11 (Cidades sustentáveis), 13 (Ação climática). |

Os moradores, casas e leituras do app são fictícios.

## Telas

| Perfil | Tela | Função |
|---|---|---|
| Todos | Login | E-mail e senha; troca do endereço do servidor. |
| ACS | Fila de visitas | Casas da microárea por urgência, aviso de dia de risco e de visitas pendentes. |
| ACS | Casa | Cor, temperatura, umidade, tendência e últimas leituras. |
| ACS | Registrar visita | Resultado da visita e observação; funciona sem internet. |
| ACS | Cadastrar residente | Dados da pessoa e da casa, com consentimento obrigatório. |
| Familiar | Minha casa | Cor, temperatura e uma frase simples sobre o que fazer. |

Prints em `prints/`.

## Como rodar

Precisa de Node 20+ e do app Expo Go no celular, na mesma rede do computador.

1. Suba a API: `dotnet run --project api/src/Termometro.Api`.
2. Abra um túnel para o celular alcançar a API: `cloudflared tunnel --url http://localhost:5080`.
3. Rode o app:
   ```
   cd app
   npm install
   cp .env.example .env      # coloque o endereço do túnel em EXPO_PUBLIC_API_URL
   npx expo start
   ```
4. Leia o QR code com o Expo Go. Contas de teste: ACS `ana.acs@demo.local`, familiar `neto@demo.local`, senha `Demo@2026!`.

## Critérios

| Critério | Onde está |
|---|---|
| Telas e navegação | Expo Router em `src/app/`, com rotas protegidas por perfil (`_layout.tsx`). |
| Consumo de dados | `src/api/cliente.ts`: chamadas à API .NET com JWT e tratamento de erros. |
| Banco de dados | SQLite no celular (`src/banco/`): visitas, cache da fila e configurações. |
| Acessibilidade | Cor sempre acompanhada de texto, contraste mínimo 4,5:1, botões grandes, rótulos para leitor de tela e fonte que acompanha o sistema. |
| Aderência ao problema | A tela principal é a decisão do ACS: quem visitar primeiro. Casa sem sinal aparece no topo. |

## Modo offline

A visita é salva primeiro no SQLite do celular, com um id próprio, e enviada quando houver internet.
O envio é repetido automaticamente e a API ignora ids repetidos, então a visita nunca é duplicada.
Sem internet, a fila e as casas já abertas continuam visíveis com a última cópia.

## Privacidade

- Token de login no armazenamento seguro do aparelho.
- CPF sempre mascarado; observação com CPF é bloqueada.
- Ao sair da conta, o cache com nomes e endereços é apagado.
- Cadastro só com consentimento (LGPD).

## Testes

```
npm run typecheck
npm test
```

Com a API no ar, `API_URL=http://localhost:5080 npm test` roda também os testes de integração.

## Limitações

- O cadastro de residente exige internet.
- Ainda não há tela para ligar um sensor novo a uma casa.
- Sem notificações push.
