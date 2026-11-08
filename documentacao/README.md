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
| 09 | [Desenho do front-end](09-front-end.md) | Direção visual (ficha decadactilar), tokens, rotas, telas, sessão, acessibilidade, marcos F1–F6 |

## Decisões de arquitetura (ADRs)

| ADR | Decisão | Status |
|---|---|---|
| [0001](adr/0001-servico-inferencia-python.md) | Inferência em serviço Python (FastAPI) chamado pela API .NET | Proposto |
| [0002](adr/0002-pipeline-ia-duas-etapas.md) | Pipeline de IA em duas etapas: CNN de padrões → indicadores → escore | Proposto |
| [0003](adr/0003-banco-postgresql.md) | PostgreSQL como banco | Aceito |
| [0004](adr/0004-captura-por-envio-de-imagem.md) | Captura por envio de imagem, um dedo por vez, ≥ 500 ppi | Proposto |
| [0005](adr/0005-publicacao-de-modelos.md) | Repositório público; pesos reais fora do Git; modelo público sintético | Aceito |
| [0006](adr/0006-autenticacao-identity-jwt.md) | Autenticação com ASP.NET Core Identity (Core) e JWT | Implementado (aceite pendente) |
| [0007](adr/0007-stack-front-end.md) | Front-end com Vite, React, TypeScript e TanStack Query; sessão na aba | Proposto |
| [0008](adr/0008-gerador-sintetico-proprio.md) | Gerador próprio de digitais sintéticas para o modelo de demonstração | Proposto |
| [0009](adr/0009-situacao-diagnostica-como-referencia.md) | Situação diagnóstica como padrão de referência: data da avaliação e declaração de independência | Proposto |
| [0010](adr/0010-hierarquia-de-acesso.md) | Hierarquia de acesso: o administrador vê os dados dos pacientes, sem ações clínicas, e atende os pedidos do titular | Proposto |
| [0011](adr/0011-protocolo-etapa2.md) | Protocolo de treino e avaliação da Etapa 2: variáveis pré-especificadas, ganho sobre sexo e idade, calibração e tamanho da amostra | Proposto |

Os ADRs ficam "Propostos" até a revisão com o orientador; ao serem aceitos,
muda-se o status no próprio arquivo. Uma decisão nova que contrarie uma antiga
gera um ADR novo que a substitui (status "Substituído por ADR-XXXX").

Diagramas em Mermaid renderizam direto no GitHub.
