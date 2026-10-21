# documentacao/

Ler na ordem abaixo antes de criar ou alterar qualquer módulo (`CLAUDE.md`, Regra 2).

| # | Documento | Conteúdo |
|---|---|---|
| 01 | [Visão e contexto](01-visao-e-contexto.md) | Problema, o que a evidência permite afirmar, objetivo, escopo, restrições éticas e regulatórias |
| 02 | [Requisitos](02-requisitos.md) | Requisitos funcionais, regras de negócio, requisitos não funcionais |
| 03 | [Casos de uso](03-casos-de-uso.md) | Atores, diagrama, especificação UC01–UC14 |
| 04 | [Diagramas UML](04-uml.md) | Classes de domínio e de serviço, sequências, estados |
| 05 | [Modelo de banco de dados](05-modelo-banco-de-dados.md) | Diagrama ER, enums, dicionário de dados, retenção |
| 06 | [Arquitetura](06-arquitetura.md) | Componentes, organização do back e do front, API REST, segurança |
| 07 | [Fundamentos da pesquisa](07-fundamentos-pesquisa.md) | Evidência científica, IA, hardware e normas, com fontes e itens a verificar |
| 08 | [Desenho do back-end](08-back-end.md) | Solução .NET 10, domínio, serviços, contratos da API, upload cifrado, cliente da IA, testes, ordem de implementação |

## Decisões de arquitetura (ADRs)

| ADR | Decisão | Status |
|---|---|---|
| [0001](adr/0001-servico-inferencia-python.md) | Inferência em serviço Python (FastAPI) chamado pela API .NET | Proposto |
| [0002](adr/0002-pipeline-ia-duas-etapas.md) | Pipeline de IA em duas etapas: CNN de padrões → indicadores → escore | Proposto |
| [0003](adr/0003-banco-postgresql.md) | PostgreSQL como banco | Aceito |
| [0004](adr/0004-captura-por-envio-de-imagem.md) | Captura por envio de imagem, um dedo por vez, ≥ 500 ppi | Proposto |
| [0005](adr/0005-publicacao-de-modelos.md) | Repositório público; pesos reais fora do Git; modelo público sintético | Aceito |
| [0006](adr/0006-autenticacao-identity-jwt.md) | Autenticação com ASP.NET Core Identity (Core) e JWT | Implementado (aceite pendente) |

Os ADRs ficam "Propostos" até a revisão com o orientador; ao serem aceitos,
muda-se o status no próprio arquivo. Uma decisão nova que contrarie uma antiga
gera um ADR novo que a substitui (status "Substituído por ADR-XXXX").

Diagramas em Mermaid renderizam direto no GitHub.
