# TCC-Dermatoglifa-TEA

Protótipo de pesquisa que transforma as 10 impressões digitais de um participante
em indicadores dermatoglíficos (padrão de cada dedo e discordância entre as mãos)
com uma rede neural treinada localmente, para investigar se esses indicadores
carregam sinal associado ao Transtorno do Espectro Autista (TEA).

> **Não é ferramenta de diagnóstico nem de triagem.** O diagnóstico de TEA é
> clínico. O sistema só pode ser usado dentro de protocolo aprovado por Comitê de
> Ética em Pesquisa. Veja [documentacao/01-visao-e-contexto.md](documentacao/01-visao-e-contexto.md).

## Estrutura

| Pasta | Conteúdo |
|---|---|
| [`treino/`](treino/) | Scripts de treino (Python + Keras), referência das aulas e registro dos modelos treinados |
| [`modelo_producao/`](modelo_producao/) | Modelo em uso pelo site e o serviço que o executa |
| [`aplicacao/front/`](aplicacao/front/) | Interface do médico (React) |
| [`aplicacao/back/`](aplicacao/back/) | API (.NET) |
| [`documentacao/`](documentacao/) | Visão, requisitos, casos de uso, UML, banco de dados, arquitetura e ADRs |

## Como rodar (desenvolvimento, só dados sintéticos)

O banco (PostgreSQL) e o Serviço de IA sobem com o Docker; a API e o front rodam
direto na máquina:

```sh
cp .env.example .env                       # troque POSTGRES_PASSWORD
docker compose up -d --build               # banco em 127.0.0.1:5432, IA em 127.0.0.1:8000
```

Depois, a API ([`aplicacao/back/README.md`](aplicacao/back/README.md), com a mesma
senha na connection string) e o front ([`aplicacao/front/README.md`](aplicacao/front/README.md)).
O Serviço de IA usa o modelo de demonstração, treinado só com dados sintéticos
(ADR-0005); para ele responder pela API, registre a versão com o `registro.sql`
da pasta do modelo (`modelo_producao/README.md`).

## Sigilo dos participantes

Dados de treino e imagens de digitais **nunca** entram neste repositório; ficam
somente na máquina local do autor. O `.gitignore` e os hooks em `.githooks/`
bloqueiam imagens, planilhas e pastas de dados. Para ativar os hooks num clone:

```sh
git config core.hooksPath .githooks
```

## Autor

Nicolas Damasceno — Trabalho de Conclusão de Curso.
