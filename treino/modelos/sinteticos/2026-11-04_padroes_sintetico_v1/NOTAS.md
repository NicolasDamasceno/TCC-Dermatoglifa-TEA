# 2026-11-04_padroes_sintetico_v1

- Origem dos dados: **SINTETICO** (gerador procedural `treino/scripts/sinteticos.py`; não deve ser interpretado).
- 570 imagens de teste, de 57 participantes.
- Acurácia balanceada no teste: 0.9861.
- Primeira versão (não há anterior).
- Erros no teste: 7 verticilos e 1 arco tendido previstos como alça direita.
- A acurácia de validação oscilou entre épocas durante o treino; investigar antes
  da próxima versão (ADR-0008, ação 2).
- Resolvido no v2 (`2026-11-05_padroes_sintetico_v2`): a causa eram as
  estatísticas do `BatchNormalization`, recalculadas agora pelo `BatchNormPreciso`.
