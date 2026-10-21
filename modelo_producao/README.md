# modelo_producao/

Guarda **o modelo que o site está usando agora**. Só existe um modelo ativo por vez.

Enquanto o repositório for público, o arquivo `modelo.keras` treinado com dados
reais fica fora do Git (ver `treino/README.md`): esta pasta versiona os
metadados e o código do serviço, e o arquivo de pesos é copiado para cá apenas
no servidor. O hash na tabela abaixo garante que o arquivo em uso é o registrado.

## Promoção de um modelo

1. O modelo foi treinado e avaliado em `treino/modelos/<versao>/`.
2. Copiar `modelo.keras`, `config.json` e `metricas.json` para esta pasta.
3. Atualizar a tabela abaixo (a linha anterior permanece como histórico).
4. Registrar a versão também na tabela `modelo_ia` do banco (ver
   `documentacao/05-modelo-banco-de-dados.md`), para que cada análise saiba
   qual modelo a produziu.

## Versão ativa

| Versão | Data | Origem (`treino/modelos/`) | Sensibilidade | Especificidade | AUC | SHA-256 |
|---|---|---|---|---|---|---|
| — | — | nenhum modelo promovido ainda | — | — | — | — |
