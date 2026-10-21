# modelo_producao/

Guarda **o modelo que o site está usando agora** e o serviço que o executa.
Só existe um modelo ativo por vez.

```
modelo_producao/
  demo/          modelo da Etapa 1 treinado só com dados sintéticos (versionado)
  servico/       Serviço de IA (FastAPI) — ADR-0001
  modelo.keras   modelo real em uso — existe SÓ no servidor, nunca no Git
```

Pela ADR-0005, pesos treinados com dados reais não entram no repositório público.
Quem clona o repositório roda o sistema com o modelo de `demo/`; no servidor da
pesquisa, o arquivo real é copiado do local controlado para `modelo.keras` e
conferido pelo hash registrado abaixo. O serviço usa `modelo.keras` se existir e
o hash conferir; caso contrário, usa `demo/` e informa isso em `GET /saude`.

## Promoção de um modelo

1. O modelo foi treinado e avaliado em `treino/modelos/reais/<versao>/` (ou
   `sinteticos/<versao>/`, para o `demo/`).
2. Copiar o `modelo.keras` do local controlado para esta pasta **no servidor** e
   conferir o hash com `modelo.sha256` da versão.
3. Atualizar a tabela abaixo (a linha anterior permanece como histórico).
4. Registrar a versão na tabela `modelo_ia` do banco (doc 05), para que cada
   análise saiba qual modelo a produziu.

## Versão ativa

| Versão | Data | Origem dos dados | Pasta em `treino/modelos/` | Métricas principais | SHA-256 |
|---|---|---|---|---|---|
| — | — | — | nenhum modelo promovido ainda | — | — |
