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
   (doc 08, seção 7). Enquanto o cadastro pela API (UC10) não existe, o registro é
   feito direto no banco.

## Versão ativa

| Versão | Data | Origem dos dados | Pasta em `treino/modelos/` | Métricas principais | SHA-256 |
|---|---|---|---|---|---|
| — | — | — | nenhum modelo promovido ainda | — | — |
