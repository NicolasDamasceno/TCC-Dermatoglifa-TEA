# ADR-0005: Repositório público, pesos reais fora do Git e modelo público sintético

**Status:** Aceito
**Data:** 2026-10-20
**Decisores:** Nicolas Damasceno (autor)

## Contexto

O repositório é público e deve continuar assim. O plano original era versionar em
`treino/` todos os modelos treinados. Porém um modelo treinado com poucas pessoas
tende a memorizar parte dos exemplos, e com o arquivo de pesos e a digital de
alguém é possível estimar se essa pessoa estava no treino — ataque de inferência
de pertencimento, mais eficaz quanto maior o sobreajuste
([Shokri et al., IEEE S&P 2017](https://arxiv.org/abs/1610.05820)). Num estudo
sobre TEA com crianças, saber que alguém participou já é dado de saúde sensível
(LGPD art. 11, §1º).

O risco não é igual nas duas etapas do pipeline (ADR-0002):
- **Etapa 1 (CNN de padrões):** aprende diretamente das imagens — é a peça com risco.
- **Etapa 2 (regressão logística):** poucos coeficientes sobre contagens
  agregadas, equivalente a publicar os resultados de um artigo — risco desprezível.

## Decisão

Adotar duas precauções:

**1. Pesos treinados com dados reais ficam fora do Git, em local controlado.**
- No repositório público ficam código, `config.json`, `metricas.json`, `NOTAS.md`
  e o **hash SHA-256** de cada versão, que prova qual arquivo foi usado.
- O arquivo `.keras` real fica em armazenamento controlado: repositório privado
  separado (sugestão: `TCC-Dermatoglifa-TEA-modelos`) ou armazenamento da
  instituição. Até essa definição, na máquina local de treino (disco cifrado) com
  backup cifrado.
- No servidor do site, o arquivo é copiado para `modelo_producao/` e conferido
  pelo hash registrado em `modelo_producao/README.md`.

**2. Versão pública da Etapa 1 treinada só com dados sintéticos.**
- Fica em `treino/modelos/sinteticos/<versao>/` e uma cópia em
  `modelo_producao/demo/`, para que qualquer clone rode o sistema completo.
- Gerada com um gerador de digitais sintéticas com rótulo de padrão. Candidatos:
  **Anguli** (gera classes de Henry, incluindo arco tendido, alças esquerda e
  direita e verticilo, com distribuição configurável;
  [tese do IISc](https://dsl.cds.iisc.ac.in/publications/thesis/sachin.pdf)) e
  **SFinGe** (usado por Peralta et al. 2017). Licenças e resolução **a verificar**
  antes do uso.
- O modelo sintético também serve como pré-treino da versão real.

**Etapa 2:** os coeficientes podem ser versionados abertamente, salvos em JSON
(`coeficientes.json`), nunca em `.pkl`.

## Opções consideradas

| Opção | Prós | Contras |
|---|---|---|
| Repositório privado | Elimina o problema de uma vez | Perde a transparência e o portfólio público |
| Publicar os pesos reais | Reprodutibilidade total | Risco de inferência de pertencimento; exige previsão no TCLE |
| **Pesos reais fora do Git + modelo sintético público (escolhida)** | Mantém o repositório público e executável; risco sob controle | Dois artefatos para manter; o modelo público tem desempenho menor |
| Treino com privacidade diferencial (DP-SGD) | Garantia formal | Com amostra pequena, perda grande de acurácia |

## Consequências

- Mais fácil: manter o projeto público e reprodutível; demonstrar o sistema sem
  dados reais.
- Mais difícil: gerenciar o local dos pesos reais e registrar hashes com cuidado.
- **Revisitar** se um dia houver interesse em publicar os pesos reais. Para isso,
  as duas condições são obrigatórias:
  1. o TCLE e o protocolo aprovado pelo CEP preverem que "o modelo treinado poderá
     ser disponibilizado publicamente";
  2. uma avaliação de inferência de pertencimento contra o próprio modelo mostrar
     desempenho do ataque equivalente ao acaso.

## Ações

1. [x] `.gitignore` e `pre-commit` bloqueiam pesos fora de `treino/modelos/sinteticos/` e `modelo_producao/demo/`.
2. [ ] Verificar licença e resolução do Anguli e do SFinGe; escolher um.
3. [ ] Definir o local controlado dos pesos reais (repositório privado ou instituição).
4. [ ] Incluir no rascunho do TCLE o tratamento do modelo treinado (não publicado).
