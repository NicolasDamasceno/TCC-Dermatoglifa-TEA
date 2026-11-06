# treino/

Tudo o que é necessário para **treinar e avaliar** os modelos de IA, exceto os dados.

```
treino/
  referencia/        modelo_arroz_keras.py — base de estrutura das aulas do 3º período
  scripts/           scripts de preparação, treino e avaliação (Python + Keras)
  modelos/
    sinteticos/      modelos treinados SÓ com dados sintéticos — pesos versionados
    reais/           modelos treinados com dados reais — só metadados, pesos fora do Git
```

## Como treinar a Etapa 1

Scripts em `scripts/` (testes em `testes/`, só com dados sintéticos):

| Script | Faz |
|---|---|
| `sinteticos.py` | gera digitais **sintéticas** rotuladas, 10 dedos por participante (ADR-0008) |
| `divisao.py` | divide treino/validação/teste **por participante** (`StratifiedGroupKFold`) e monta uma pasta por classe com links |
| `etapa1.py` | treina a CNN de padrões na estrutura do `modelo_arroz_keras`, avalia no teste e grava a versão |

```sh
python -m venv .venv && . .venv/bin/activate      # Windows: .venv\Scripts\activate
pip install -r treino/requirements.txt
pip install -e modelo_producao/servico --no-deps  # pré-processamento comum (ADR-0001)
export TCC_DADOS_DIR=/caminho/local/fora/do/repositorio

# modelo de demonstração (dados sintéticos)
python treino/scripts/sinteticos.py --participantes 400
python treino/scripts/etapa1.py --origem SINTETICO --promover-demo

# com a coleta aprovada pelo CEP: $TCC_DADOS_DIR/reais/rotulos.csv
# (colunas arquivo, participante, classe; arquivo relativo ao CSV)
python treino/scripts/etapa1.py --dados "$TCC_DADOS_DIR/reais" --origem REAL \
    --pesos-reais /local/controlado/modelos
```

Cada execução grava `modelos/<sinteticos|reais>/<versao>/` (tabela abaixo) e, na
pasta de dados local, `saidas/<versao>/` com a divisão por participante e uma figura
de previsões. Essas duas ficam fora do Git: têm códigos de participante e imagens.
O treino imprime a matriz de confusão e a sensibilidade, a especificidade e a AUC
de cada classe; `registro.sql` registra a versão na tabela `modelo_ia` da API.

Antes de cada validação, e mais uma vez no fim, o treino recalcula a média e a
variância de cada `BatchNormalization` sobre o treino sem augmentation
(`BatchNormPreciso`, ADR-0002). A média móvel padrão do Keras ficava atrasada em
relação aos pesos: no v1 a validação oscilava entre 0,77 e 0,98 de uma época para
outra e o `EarlyStopping` e o `ReduceLROnPlateau` reagiam a esse ruído. No v2,
com a correção e a mesma semente, a validação subiu sem oscilar e ficou estável
a partir da época 11.

Em CPU (Ryzen 5 5600, ADR-0002) o treino é viável; o tempo depende do tamanho de
entrada (`--tamanho`, padrão 128 px) e do número de imagens.

## Os dados não ficam aqui

As imagens de digitais e os metadados dos participantes ficam **apenas na máquina
local**, fora do repositório. Os scripts recebem o caminho por variável de
ambiente, por exemplo:

```sh
export TCC_DADOS_DIR=/caminho/local/fora/do/repositorio
```

O `.gitignore` e o hook `pre-commit` bloqueiam imagens, planilhas e pastas
`dados/`, `dataset*/` caso algo seja copiado para cá por engano.

## Código comum com o serviço

O pré-processamento, as classes e os indicadores ficam no pacote `dermatoglifia_ia`,
em `modelo_producao/servico/`, para que treino e produção nunca divirjam
(ADR-0001). O treino o instala no mesmo ambiente:

```sh
pip install -r modelo_producao/servico/requirements.txt
pip install -e modelo_producao/servico --no-deps
```

e carrega as imagens com `image_dataset_from_directory(**argumentos_dataset(...))`.

## Pesos dos modelos (ADR-0005)

O repositório é público. Um modelo treinado com digitais reais pode revelar quem
participou do estudo (inferência de pertencimento), por isso:

| Tipo de modelo | Pasta | O que vai para o Git | Onde ficam os pesos |
|---|---|---|---|
| Etapa 1 treinada com dados **sintéticos** | `modelos/sinteticos/<versao>/` | tudo, inclusive `modelo.keras` | no Git |
| Etapa 1 treinada com dados **reais** | `modelos/reais/<versao>/` | config, métricas, hash, notas | local controlado (repositório privado ou instituição; até lá, disco local cifrado com backup cifrado) |
| Etapa 2 (regressão logística) | `modelos/reais/<versao>/` | também os coeficientes em `coeficientes.json` | no Git (são estatísticas agregadas) |

O `.gitignore` e o `pre-commit` rejeitam qualquer `.keras`, `.h5`, `.onnx`, `.pt`
ou `.tflite` fora de `modelos/sinteticos/` e `modelo_producao/demo/`. Modelos
nunca são salvos em `.pkl`.

Publicar pesos reais só será reavaliado se o TCLE aprovado pelo CEP previr isso
**e** uma avaliação de inferência de pertencimento mostrar risco equivalente ao acaso.

## Como registrar um modelo treinado

Cada treino gera uma pasta `AAAA-MM-DD_<nome>_v<N>/` em `sinteticos/` ou `reais/`:

| Arquivo | Conteúdo |
|---|---|
| `modelo.keras` | pesos e arquitetura (só no Git em `sinteticos/`; em `reais/`, vai para `--pesos-reais`) |
| `modelo.sha256` | hash SHA-256 do `modelo.keras` |
| `metricas.json` | matriz de confusão, recall por classe; na Etapa 2, sensibilidade, especificidade, AUC |
| `config.json` | hiperparâmetros, seed, versão do Keras e, obrigatoriamente, o que o serviço lê: `versao`, `classes` (na ordem da saída), `tamanho_imagem`, `ajuste_proporcao`, `interpolacao`, `origem_dados` (formato em `modelo_producao/servico/README.md`) |
| `coeficientes.json` | apenas na Etapa 2 |
| `graficos/` | curvas de acurácia/perda e matriz de confusão em **SVG** (PNG é bloqueado pelo `.gitignore`) |
| `registro.sql` | registra e ativa a versão na tabela `modelo_ia` da API, sem interface (a tela **Modelos de IA**, UC10, faz o mesmo com o `config.json` e o `metricas.json`) |
| `NOTAS.md` | o que mudou em relação à versão anterior |

Métricas, configurações e notas não podem conter identificadores de participantes.
