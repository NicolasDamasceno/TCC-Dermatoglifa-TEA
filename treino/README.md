# treino/

Tudo o que é necessário para **treinar e avaliar** os modelos de IA, exceto os dados.

```
treino/
  referencia/   modelo_arroz_keras.py — base de estrutura das aulas do 3º período
  scripts/      scripts de preparação, treino e avaliação (Python + Keras)
  modelos/      cada modelo treinado, em sua própria pasta versionada
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

## Pesos treinados e repositório público

Um modelo treinado com digitais reais carrega informação dos participantes; em
um repositório **público**, isso abre risco de inferir quem participou do estudo
(um dado de saúde sensível). Por isso, por padrão, o `.gitignore` bloqueia
`*.keras` e o repositório guarda apenas `config.json`, `metricas.json`, o hash
SHA-256 e as notas de cada versão — os pesos ficam na máquina local, junto dos dados.

Para versionar os próprios arquivos `.keras` aqui, uma das condições deve valer:
- o repositório passar a ser **privado**; ou
- o modelo ter sido treinado só com dados sintéticos ou públicos de licença compatível.

Nesse caso, remover as linhas de pesos do `.gitignore` em um commit próprio,
registrando o motivo.

## Como registrar um modelo treinado

Cada treino gera uma pasta em `modelos/` com o padrão
`AAAA-MM-DD_<nome>_v<N>/` contendo:

| Arquivo | Conteúdo |
|---|---|
| `modelo.keras` | pesos e arquitetura (local; ver seção acima) |
| `metricas.json` | sensibilidade, especificidade, AUC, matriz de confusão (teste) |
| `config.json` | hiperparâmetros, tamanho de imagem, classes, seed, versão do Keras |
| `graficos/` | curvas de acurácia/perda (sem imagens de digitais) |
| `NOTAS.md` | o que mudou em relação à versão anterior |

Métricas e configurações não podem conter identificadores de participantes.
