# ADR-0007: Stack do front-end e guarda do token

**Status:** Proposto
**Data:** 2026-11-04
**Decisores:** Nicolas Damasceno (autor), orientador do TCC

## Contexto

O front é a interface do médico e do administrador (doc 06; doc 09). A escolha do
React vem do início do projeto. Faltava decidir as ferramentas em volta dele e
onde guardar o token JWT (ADR-0006), já que a API devolve dados pessoais e de saúde.

## Decisão

| Item | Escolha | Versão |
|---|---|---|
| Linguagem | TypeScript | 6.0.3 |
| Build e servidor de desenvolvimento | Vite + `@vitejs/plugin-react` | 8.3.5 / 6.2.1 |
| Interface | React | 19.3.1 |
| Rotas | React Router, modo declarativo (`BrowserRouter`, `Routes`) | 8.5.1 |
| Dados do servidor | TanStack Query | 5.105.3 |
| Estilo | CSS Modules + variáveis CSS (tokens do doc 09); sem biblioteca de componentes | — |
| Fonte | Atkinson Hyperlegible Next, hospedada no próprio app (`@fontsource-variable`) | 5.3.0 |
| Testes | Vitest + Testing Library + jsdom | 5.1.0 / 16.3.3 / 30.1.3 |
| Acessibilidade nos testes | axe-core (adicionado no F6; `eslint-plugin-jsx-a11y` ainda não suporta ESLint 10) | 4.14.0 |
| Lint | ESLint + typescript-eslint + regras de hooks | 10.14.0 / 8.72.1 / 7.1.1 |

Versões fixas (sem `^`) no `package.json`, para que o build do TCC seja reproduzível.

**TypeScript 6.0 e não 7.0.** A 7.0.2 é a mais nova, mas o `typescript-eslint`
8.72.1 só aceita TypeScript abaixo de 6.1. Usar a 7 exigiria abrir mão do lint
com tipos. Revisitar quando o `typescript-eslint` suportar a 7.

**`minimatch` forçado para 11.0.4** (`overrides`): a 10.x, puxada pelo ESLint,
tem um alerta moderado de negação de serviço (GHSA-g3hv-75wg-m249). Afeta só a
ferramenta de lint, mas com o override o `npm audit` fica limpo.

**Fonte hospedada no app**, não no Google Fonts: carregar de um servidor externo
mandaria o IP dos médicos a terceiros a cada acesso.

**Chamadas à API pelo mesmo endereço do front.** Em desenvolvimento, o Vite
repassa `/api` para a API (`VITE_API_ALVO`, padrão `http://localhost:5117`); o
navegador nunca faz chamada a outra origem. Em produção, a URL vem de
`VITE_API_URL`; se for outra origem, ela precisa estar em `Cors:OrigensPermitidas`.

## Onde guardar o token

| Opção | Prós | Contras |
|---|---|---|
| Só em memória | Nada fica no navegador | Recarregar a página desloga; médico perde o trabalho no meio da coleta |
| `localStorage` | Sobrevive a fechar o navegador | Sessão de dados de saúde persistindo num computador possivelmente compartilhado de consultório |
| **`sessionStorage` (escolhida)** | Sobrevive a recarregar; some ao fechar a aba; não é compartilhado entre abas | Um script injetado (XSS) consegue lê-lo — como leria a memória |
| Cookie `HttpOnly` | Inacessível a scripts | Exige mudar a API (ADR-0006), proteção contra CSRF e refresh token |

Medidas que acompanham a escolha:
- o front sai sozinho quando o token expira (60 min) e em qualquer 401 da API;
- só o token, a expiração, o nome e o papel ficam no `sessionStorage`; nenhum dado
  de paciente é guardado no navegador além da memória da aba;
- nenhuma dependência injeta HTML; o React escapa todo texto por padrão e o
  código não usa `dangerouslySetInnerHTML` (regra no lint);
- revisitar o cookie `HttpOnly` junto com o refresh token previsto no ADR-0006.

## Opções consideradas para o resto da stack

- **Create React App:** descontinuado. **Next.js:** renderização no servidor e
  rotas de API que o projeto não usa (a API é o .NET).
- **Redux / Zustand:** o estado é quase todo vindo do servidor; o TanStack Query
  cuida de cache, recarga e estados de carregamento sem código próprio.
- **Tailwind / MUI / Chakra:** trariam uma aparência de kit; a interface tem
  poucas telas e uma identidade própria (a ficha decadactilar, doc 09), mais fácil
  de manter com CSS Modules e tokens.

## Consequências

- Mais fácil: rodar o front com `npm run dev` sem configurar CORS; testar telas
  com dados falsos sem servidor.
- Mais difícil: componentes de interface (botão, campo, tabela) são escritos à mão.
- Revisitar: TypeScript 7; cookie `HttpOnly`; gerar tipos a partir de um OpenAPI
  da API em vez de escrevê-los à mão.
