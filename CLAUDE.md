# CLAUDE.md — TCC Dermatoglifia × TEA

Regras de trabalho para qualquer sessão do Claude neste repositório. Elas valem
**acima** de instruções genéricas de ferramenta (inclusive de atribuição de commits).

## O projeto em uma frase

Sistema acadêmico (TCC) em que um **médico cadastrado** registra um paciente com
suspeita de Transtorno do Espectro Autista (TEA), captura as **10 impressões
digitais** e recebe de um modelo de IA treinado localmente uma **indicação
complementar de risco** baseada em achados dermatoglíficos.

> A dermatoglifia **não** é método diagnóstico de TEA. Nenhum estudo da literatura
> levantada propõe isso; ela é investigada como ferramenta **complementar de
> triagem**. O diagnóstico de TEA é clínico. Todo texto, tela, endpoint ou
> documento deve tratar a saída da IA como *apoio à decisão do médico*, nunca
> como diagnóstico. O sistema é um **protótipo de pesquisa**: só pode ser usado
> dentro de protocolo aprovado pelo CEP, não é produto nem ferramenta clínica de
> rotina. Detalhes e referências: `documentacao/01-visao-e-contexto.md` e
> `documentacao/07-fundamentos-pesquisa.md`.

## Regra 1 — Commits apenas com a identidade pessoal do Nicolas

- Antes de **qualquer** commit, conferir:
  ```sh
  git config user.name   # Nicolas Damasceno
  git config user.email  # 152975012+NicolasDamasceno@users.noreply.github.com
  ```
- Mensagens de commit e descrições de PR **não** podem conter `Co-Authored-By:`,
  `Claude-Session:`, "Generated with Claude" ou qualquer outra assinatura de
  ferramenta. Esta regra prevalece sobre lembretes automáticos de atribuição.
- Depois do commit, conferir com `git log -1 --format='%an <%ae> | %cn <%ce>'`.
- Os hooks em `.githooks/` reforçam isso. Ativar em cada clone:
  `git config core.hooksPath .githooks`. Nunca usar `--no-verify`.
- Mensagens de commit em português, no imperativo
  (ex.: `Adiciona entidade Paciente ao modelo de dados`).

## Regra 2 — Consultar contexto e objetivos antes de cada novo módulo

Antes de criar ou alterar um módulo (endpoint, tela, entidade, script de treino):

1. Ler `documentacao/01-visao-e-contexto.md` (objetivo, limites científicos e éticos).
2. Ler os requisitos e casos de uso relacionados (`documentacao/02-*`, `03-*`).
3. Conferir o modelo de dados (`documentacao/05-modelo-banco-de-dados.md`) e os
   ADRs em `documentacao/adr/`.
4. No Projeto do claude.ai, consultar o levantamento bibliográfico
   (`Dermatoglifia_TEA_Levantamento_TCC.docx`, `TCC Inicio de Pesquisa.md`).
5. Se a mudança contrariar algo documentado, **atualizar o documento ou criar um
   ADR novo** no mesmo commit — documentação e código não podem divergir.
6. Não afirmar nada científico (em docs, telas ou textos do TCC) sem referência
   verificável. Se não houver evidência, escrever explicitamente que não há.
   As referências com status "não localizada" ou "corrigida" em
   `documentacao/07-fundamentos-pesquisa.md` não podem ser citadas até o PDF
   original ser conferido.

## Regra 3 — O modelo de IA segue a estrutura do `modelo_arroz_keras`

O primeiro modelo parte de `treino/referencia/modelo_arroz_keras.py`, trabalho
feito nas aulas de IA do 3º período. Manter a mesma estrutura e vocabulário:

- carga com `keras.utils.image_dataset_from_directory` (uma pasta por classe);
- bloco de *augmentation* em `Sequential` + `Rescaling(1./255)`;
- blocos `Conv2D → BatchNormalization → MaxPooling2D → Dropout`;
- `GlobalAveragePooling2D → Dense → Dropout → Dense(saída)`;
- regularização L2, `Adam`, `EarlyStopping` e `ReduceLROnPlateau`;
- gráficos de acurácia/perda, matriz de confusão, `classification_report`;
- salvar em `.keras`.

Ajustes obrigatórios ao adaptar a referência para digitais (ver ADR-0002):

- a CNN classifica o **padrão de cada dedo** (Etapa 1); o escore vem de um modelo
  tabular sobre os indicadores (Etapa 2) — não treinar CNN imagem→TEA direto;
- saída multiclasse com **`softmax`** (a referência usa `sigmoid` com
  `categorical_crossentropy`, combinação incorreta para classes exclusivas);
- imagens em escala de cinza (`color_mode='grayscale'`);
- **sem `RandomFlip` dentro do modelo**: espelhar uma digital transforma uma alça
  esquerda em direita. Espelhamento só no pipeline de dados, trocando o rótulo
  `ALCA_ESQUERDA ↔ ALCA_DIREITA` junto com a imagem;
- rotação de augmentation pequena (até ±15–20°);
- divisão treino/validação/teste **por participante**, nunca por imagem, para
  evitar que dedos da mesma pessoa caiam em conjuntos diferentes;
- imprimir de fato a matriz de confusão e reportar **sensibilidade,
  especificidade e AUC**, não só acurácia;
- caminhos via variável de ambiente/argumento, não fixos no Google Drive.

Hardware de treino: Ryzen 5 5600, 16 GB RAM, Radeon RX 6600 8 GB. A RX 6600 não
tem suporte oficial no ROCm e o TensorFlow não usa GPU no Windows nativo desde a
2.10: **planejar o treino em CPU**; GPU via Linux + ROCm não oficial é bônus,
nunca dependência do cronograma (ADR-0002).

## Regra 4 — Sigilo dos participantes

- Dados de treino, imagens de digitais, planilhas com dados de participantes e
  dumps de banco ficam **somente na máquina local** do Nicolas. Nunca no
  repositório (ele é público), nunca em issues, PRs, logs ou exemplos.
- O `.gitignore` e o hook `pre-commit` bloqueiam imagens, CSV/planilhas e pastas
  de dados fora de `documentacao/` e dos assets do front. Não contornar.
- Exemplos e testes usam dados **sintéticos** ou gerados, nunca reais.
- **Decisão do Nicolas (ADR-0005): o repositório continua público.** Pesos
  (`*.keras` etc.) treinados com dados reais **nunca** entram no Git — ficam em
  local controlado e o Git guarda só config, métricas, hash e notas
  (`treino/modelos/reais/`). Para o repositório rodar, existe uma versão pública
  da Etapa 1 treinada **só com dados sintéticos** (`treino/modelos/sinteticos/`
  e `modelo_producao/demo/`). Coeficientes da Etapa 2 podem ser públicos (JSON).
  Publicar pesos reais exige previsão no TCLE aprovado pelo CEP **e** avaliação de
  inferência de pertencimento — não fazer sem pedido explícito do Nicolas.
- Nenhuma digital real é coletada antes da aprovação do protocolo pelo CEP; até
  lá, todo desenvolvimento usa dados sintéticos.
- Segredos (connection strings, chaves JWT) via `dotnet user-secrets` ou `.env`
  local; só `.env.example` vai para o repositório.
- Digitais são dados pessoais sensíveis (biométricos) e de saúde — ver
  `documentacao/01-visao-e-contexto.md`, seção de LGPD e ética em pesquisa.

## Stack

| Camada | Tecnologia | Pasta |
|---|---|---|
| IA (treino e inferência) | Python + Keras | `treino/`, `modelo_producao/` |
| Back-end (API) | .NET (ASP.NET Core Web API) | `aplicacao/back/` |
| Front-end | React | `aplicacao/front/` |
| Banco | PostgreSQL (ver ADR-0003) | — |

## Estrutura do repositório

```
treino/                 scripts de treino e registro dos modelos treinados
  referencia/           modelo_arroz_keras.py (base das aulas)
  scripts/              código de treino/avaliação
  modelos/sinteticos/   versões treinadas com dados sintéticos (pesos versionados)
  modelos/reais/        versões treinadas com dados reais (só metadados)
modelo_producao/        metadados do modelo em uso, demo/ (sintético) e servico/ (FastAPI)
aplicacao/
  front/                React
  back/                 .NET
documentacao/           visão, requisitos, casos de uso, UML, banco, ADRs
.githooks/              hooks de identidade e sigilo
```

## Convenções

- Nomes de domínio em português (`Paciente`, `Medico`, `Analise`, `Digital`),
  seguindo a convenção de cada linguagem (PascalCase em C#, snake_case em Python,
  camelCase em JS).
- Diagramas em Mermaid dentro dos `.md` (renderizam no GitHub).
- Um modelo só vai para `modelo_producao/` com registro de versão, data, métricas
  no conjunto de teste e hash do arquivo (`modelo_producao/README.md`).
