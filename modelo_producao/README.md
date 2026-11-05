# modelo_producao/

Guarda **o modelo que o site está usando agora** e o serviço que o executa.
Só existe um modelo ativo por vez.

```
modelo_producao/
  demo/          modelo da Etapa 1 treinado só com dados sintéticos (versionado)
  servico/       Serviço de IA (FastAPI) e código comum de IA — ADR-0001
  producao/      modelo real em uso — existe SÓ no servidor, nunca no Git
```

Pela ADR-0005, pesos treinados com dados reais não entram no repositório público.
Quem clona o repositório roda o sistema com o modelo de `demo/`. No servidor da
pesquisa, `producao/` recebe `modelo.keras` (copiado do local controlado),
`modelo.sha256` e `config.json` (de `treino/modelos/reais/<versao>/`). O serviço
usa `producao/` sempre que a pasta existir e confere o hash; se algo estiver
errado, ele **não** cai para o `demo/`: fica indisponível e explica o motivo em
`GET /saude` (detalhes em `servico/README.md`).

## Promoção de um modelo

1. O modelo foi treinado e avaliado em `treino/modelos/reais/<versao>/` (ou
   `sinteticos/<versao>/`, para o `demo/`).
2. Copiar para `producao/`, **no servidor**, o `modelo.keras` do local controlado e
   o `modelo.sha256` e o `config.json` da versão; o serviço confere o hash ao subir.
3. Atualizar a tabela abaixo (a linha anterior permanece como histórico).
4. Registrar a versão na tabela `modelo_ia` do banco (doc 05) e marcá-la como a
   única ativa. A API recusa respostas de versões não registradas ou inativas
   (doc 08, seção 7). Pela tela **Modelos de IA** do administrador (UC10): ela
   mostra o que o serviço carregou e pede o `config.json` e o `metricas.json` da
   versão; o hash vem do próprio serviço. Sem interface, o `registro.sql` da pasta
   da versão faz o mesmo direto no banco.

## Versão ativa

| Versão | Data | Origem dos dados | Pasta em `treino/modelos/` | Métricas principais | SHA-256 |
|---|---|---|---|---|---|
| `2026-11-04_padroes_sintetico_v1` (em `demo/`) | 2026-11-04 | Sintético | `sinteticos/2026-11-04_padroes_sintetico_v1` | acurácia balanceada 0,986 no teste **sintético** (570 imagens, 57 participantes) | `56ba5d66dfed470a9930ea020c3fd7e52c8d25f2fb06fb7005f7cd6de1a989b2` |

O modelo de `demo/` serve só para o sistema rodar: suas métricas não dizem nada
sobre digitais reais (ADR-0008). Nenhum modelo real foi treinado ainda.
