# 2026-11-05_padroes_sintetico_v2

- Origem dos dados: **SINTETICO** (gerador procedural `treino/scripts/sinteticos.py`; não deve ser interpretado).
- 570 imagens de teste, de 57 participantes (mesma divisão e mesma semente, 42, do v1).
- Acurácia balanceada no teste: 1.0000 (v1: 0.9861). Nenhum erro na matriz de confusão.
- O que mudou em relação ao v1:
  - `BatchNormPreciso` (PreciseBN; ADR-0002): a média e a variância de cada
    `BatchNormalization` são recalculadas sobre o treino sem augmentation antes de
    cada validação e no fim do treino. A validação deixou de oscilar: subiu de 0,72
    para 0,98 até a época 6 e ficou em 1,0 a partir da época 11, com `val_loss`
    caindo de forma contínua até 0,140 (v1: melhor `val_loss` 0,333).
  - Máximo de 30 épocas (v1: 60) por limite de tempo do ambiente em que foi treinado.
    O treino parou no limite, não pelo `EarlyStopping`: a `val_loss` ainda caía
    devagar na época 30.
- Os erros do v1 (7 verticilos e 1 arco tendido previstos como alça direita) não
  aparecem mais.
- Acertar 100% do teste sintético mostra que o gerador é fácil de separar, não que
  o modelo reconheça digitais reais (ADR-0008).
