# aplicacao/front/

Interface em **React** usada pelo médico e pela administração do protocolo.
Desenho completo em [`documentacao/09-front-end.md`](../../documentacao/09-front-end.md)
e decisão da stack no [ADR-0007](../../documentacao/adr/0007-stack-front-end.md).
Antes de alterar qualquer tela, ler o `CLAUDE.md` da raiz.

> Protótipo de pesquisa. A saída da IA é apoio à decisão do médico, nunca
> diagnóstico; a faixa de pesquisa aparece em todas as telas.

## Stack

Vite 8 · React 19 · TypeScript 6 · React Router 8 · TanStack Query 5 ·
CSS Modules com tokens em variáveis CSS · fonte Atkinson Hyperlegible Next
(servida pelo próprio app, sem CDN) · Vitest + Testing Library.

## Como rodar

Requer Node 22+ e a API rodando (ver `aplicacao/back/README.md`).

```sh
cd aplicacao/front
npm ci
cp .env.example .env.local   # opcional: só se a API não estiver em http://localhost:5117
npm run dev                  # http://localhost:5173
```

Em desenvolvimento o Vite repassa `/api` para a API, então o navegador fala com
uma origem só. O CORS da API já aceita `http://localhost:5173`.

| Comando | O que faz |
|---|---|
| `npm run dev` | servidor de desenvolvimento |
| `npm test` | testes (Vitest, jsdom) |
| `npm run lint` | ESLint (inclui proibição de `console` e de `dangerouslySetInnerHTML`) |
| `npm run typecheck` | TypeScript estrito |
| `npm run build` | build de produção em `dist/` (não versionado) |

## Variáveis

| Variável | Uso |
|---|---|
| `VITE_API_ALVO` | destino do proxy `/api` em desenvolvimento (padrão `http://localhost:5117`) |
| `VITE_API_URL` | prefixo das chamadas em produção; vazio = mesma origem |

Nenhum segredo vai para o front: tudo que começa com `VITE_` fica visível no navegador.

## Sessão e dados

- O token fica no `sessionStorage` da aba (some ao fechar) e a sessão termina
  sozinha quando ele expira ou em qualquer 401 da API.
- Ao sair, o cache de consultas é apagado: nenhum dado de paciente sobrevive à sessão.
- Nada de dados de paciente no console (regra de lint) nem em testes: os testes usam
  usuários e dados sintéticos.

## Marcos

| Marco | Entrega | Situação |
|---|---|---|
| F1 | Base, sessão, rotas por papel, layout com faixa de pesquisa, tela Entrar | Concluído |
| F2 | Solicitar cadastro e cadastros pendentes (UC02, UC03) | — |
| F3 | Pacientes e consentimentos (UC04, UC05) | — |
| F4 | Coleta com a ficha decadactilar (UC06) | — |
| F5 | Resultado da análise (UC07, UC08) | — |
| F6 | Revisão de acessibilidade e build | — |
