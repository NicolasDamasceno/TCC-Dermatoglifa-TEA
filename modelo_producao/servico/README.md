# modelo_producao/servico/

Serviço de IA do sistema (ADR-0001) e o código de IA comum ao treino e à
inferência. A API .NET é o único cliente.

```
servico/
  dermatoglifia_ia/     código comum: o treino (treino/scripts) importa este mesmo pacote
    dominio.py          mãos, dedos, classes, conversão de alças e indicadores (ADR-0002)
    preprocessamento.py PNG → tensor, com as mesmas chamadas do image_dataset_from_directory
    modelo.py           pasta do modelo: config.json, hash e carga segura do .keras
  servico/              FastAPI: POST /inferencia e GET /saude
  testes/               pytest, só com modelos e imagens sintéticos
```

> O serviço recebe 10 imagens de digitais, que são dados biométricos. Ele não
> grava nada em disco, não acessa o banco, não registra o conteúdo das imagens e
> escuta só em `127.0.0.1` por padrão. Se for para outra máquina, use HTTPS
> (ADR-0001).

## Como rodar

Requer Python 3.11 a 3.13. No Windows nativo o TensorFlow roda só em CPU, o que
basta para a inferência (ADR-0002).

```sh
cd modelo_producao/servico
python -m venv .venv
. .venv/bin/activate            # Windows: .venv\Scripts\activate
pip install -r requirements.txt
pip install -e . --no-deps      # instala dermatoglifia_ia (o treino usa o mesmo)
python -m servico               # http://127.0.0.1:8000
```

A API .NET chama `Inferencia:UrlBase` (padrão `http://localhost:8000`).

### Com Docker

O `compose.yaml` da raiz constrói este `Dockerfile` e sobe o serviço em
`127.0.0.1:8000`, junto com o PostgreSQL (`docker compose up -d --build`). Na
imagem não há modelo: o compose monta `modelo_producao/demo/` em `/modelos/demo`,
somente leitura. O contêiner roda sem privilégios e com a raiz só de leitura; o
Keras grava a própria configuração em `$HOME`, que aponta para o `/tmp` em
memória. O `HEALTHCHECK` consulta `/saude` e marca o contêiner como
"unhealthy" quando não há modelo válido. No servidor da pesquisa:

```sh
docker compose -f compose.yaml -f compose.producao.yaml up -d --build
```

O `compose.producao.yaml` monta `modelo_producao/producao/` e não sobe se a pasta
não existir.

| Variável | Padrão | Uso |
|---|---|---|
| `DERMATOGLIFIA_MODELO_PRODUCAO` | `modelo_producao/producao/` | pasta do modelo real (só no servidor da pesquisa; fora do Git) |
| `DERMATOGLIFIA_MODELO_DEMO` | `modelo_producao/demo/` | pasta do modelo treinado só com dados sintéticos |
| `DERMATOGLIFIA_CONVENCAO_ALCA` | `IMPRESSAO` | orientação das imagens; **igual** a `Inferencia:ConvencaoAlca` da API |
| `DERMATOGLIFIA_LIMIAR_CONFIANCA` | `0.5` | abaixo disso, o dedo recebe `alerta_qualidade` |
| `DERMATOGLIFIA_LIMIAR_CONTRASTE` | `10` | desvio-padrão mínimo dos tons de cinza; abaixo, `alerta_qualidade` |
| `DERMATOGLIFIA_HOST`, `DERMATOGLIFIA_PORTA` | `127.0.0.1`, `8000` | onde escutar |

## Qual modelo é usado

1. Se a pasta de produção existe e tem arquivos, ela é usada. Se o modelo dali
   for inválido (hash, formato, saída sem softmax), o serviço **não** cai para o
   de demonstração: responde 503 e explica o motivo em `/saude`. No servidor da
   pesquisa, responder com um modelo sintético seria pior do que não responder.
2. Sem pasta de produção, usa `modelo_producao/demo/`.
3. Sem nenhum dos dois, o serviço sobe, mas `/saude` e `/inferencia` respondem 503.

Uma pasta de modelo tem `modelo.keras`, `modelo.sha256` e `config.json`:

```json
{ "versao": "2026-11-15_padroes_v1",
  "classes": ["ALCA_DIREITA", "ALCA_ESQUERDA", "ARCO", "ARCO_TENDIDO", "VERTICILO"],
  "tamanho_imagem": [128, 128],
  "ajuste_proporcao": "ESTICAR",
  "interpolacao": "bilinear",
  "origem_dados": "SINTETICO" }
```

`classes` vem na ordem da saída da rede (a do `image_dataset_from_directory`, que
é alfabética). `ajuste_proporcao` e `interpolacao` são opcionais e precisam ser os
mesmos do treino: `argumentos_dataset()` os transforma nos parâmetros do Keras.
Não troque o redimensionamento por outro "equivalente": com o modelo de
demonstração, carregar as mesmas digitais com `keras.utils.load_img` (PIL, com
antialias) em vez de `tf.image.resize` muda a confiança em até 0,26.
O `.keras` é lido com `safe_mode=True`: um arquivo com código Python embutido
(camada `Lambda`) é recusado.

A versão também precisa estar registrada e ativa na tabela `modelo_ia` da API,
que recusa respostas de versões desconhecidas (doc 08, seção 7).

## Rotas

`GET /saude` → `200` com `versao_modelo`, `origem` (`PRODUCAO`/`DEMO`),
`origem_dados`, `sha256`, `tamanho_imagem` e `convencao_alca`; `503` com `motivo`
quando não há modelo.

`POST /inferencia` → contrato completo no ADR-0001. Recusa com `422` o que foge
do contrato: falta de dedo, campo a mais (por exemplo, um nome), conteúdo que não
é PNG, sexo ou idade inválidos. Não há páginas `/docs` nem `/openapi.json`.

`escore_risco` é sempre `null`: a Etapa 2 ainda não foi treinada nem validada
(ADR-0002, RN10). Sexo e idade são conferidos, mas só serão usados por ela.

**Alerta de qualidade.** Até a adoção do NFIQ 2 (ADR-0004), o dedo é marcado
quando a imagem é quase uniforme ou quando a CNN fica indecisa. Os dois limiares
são escolhas de engenharia, configuráveis e **não validadas**; o alerta pede ao
médico que confira o dedo.

## Testes

```sh
pip install -r requirements-dev.txt
pytest          # 43 testes
ruff check . && ruff format --check .
```

O teste mais importante é `test_preprocessamento.py`: grava PNGs sintéticos de
tamanhos variados numa pasta por classe, carrega com `image_dataset_from_directory`
(como o treino) e confere, pixel a pixel, que o serviço produz o mesmo lote nos
três modos de ajuste de proporção.

O caminho inteiro também foi conferido em 04/11/2026: envio pela API .NET,
normalização, cifragem, leitura e inferência deram as mesmas probabilidades que o
modelo calculado direto sobre os arquivos originais, nos 10 dedos. Esse teste achou
um defeito na normalização do back, que clareava os tons de cinza, já corrigido
(doc 08, seção 6).
