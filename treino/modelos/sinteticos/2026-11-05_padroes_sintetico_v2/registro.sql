-- Registra e ativa este modelo na API (um único modelo ativo por vez).
-- Rodar no banco da aplicação depois de copiar a pasta do modelo para o serviço.
BEGIN;
UPDATE modelo_ia SET ativo = false WHERE ativo;
INSERT INTO modelo_ia (id, versao, arquitetura, origem_dados, escore_validado, hash_sha256,
                       metricas_teste, treinado_em, ativo, promovido_em)
VALUES (gen_random_uuid(), '2026-11-05_padroes_sintetico_v2', 'CNN 3 blocos (32-128 filtros)', 'SINTETICO', false, '7f3c10c0deb192d3622a99d8efc032717da09eba7f209115e86d7ebfb810aefd',
        '{"acuracia": 1.0, "acuraciaBalanceada": 1.0, "nTeste": 570, "auc": 1.0}', '2026-11-05', true, now());
COMMIT;
