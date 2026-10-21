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

## Os dados não ficam aqui

As imagens de digitais e os metadados dos participantes ficam **apenas na máquina
local**, fora do repositório. Os scripts recebem o caminho por variável de
ambiente, por exemplo:

```sh
export TCC_DADOS_DIR=/caminho/local/fora/do/repositorio
```

O `.gitignore` e o hook `pre-commit` bloqueiam imagens, planilhas e pastas
`dados/`, `dataset*/` caso algo seja copiado para cá por engano.

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
| `modelo.keras` | pesos e arquitetura (só no Git em `sinteticos/`) |
| `modelo.sha256` | hash SHA-256 do `modelo.keras` |
| `metricas.json` | matriz de confusão, recall por classe; na Etapa 2, sensibilidade, especificidade, AUC |
| `config.json` | hiperparâmetros, tamanho de imagem, classes, seed, versão do Keras, origem dos dados (sintético/real) |
| `coeficientes.json` | apenas na Etapa 2 |
| `graficos/` | curvas de acurácia/perda em **SVG** (PNG é bloqueado pelo `.gitignore`) |
| `NOTAS.md` | o que mudou em relação à versão anterior |

Métricas, configurações e notas não podem conter identificadores de participantes.
