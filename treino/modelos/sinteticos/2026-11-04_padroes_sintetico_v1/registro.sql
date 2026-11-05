-- Registra e ativa este modelo na API (um único modelo ativo por vez).
-- Rodar no banco da aplicação depois de copiar a pasta do modelo para o serviço.
BEGIN;
UPDATE modelo_ia SET ativo = false WHERE ativo;
INSERT INTO modelo_ia (id, versao, arquitetura, origem_dados, escore_validado, hash_sha256,
                       metricas_teste, treinado_em, ativo, promovido_em)
VALUES (gen_random_uuid(), '2026-11-04_padroes_sintetico_v1', 'CNN 3 blocos (32-128 filtros)', 'SINTETICO', false, '56ba5d66dfed470a9930ea020c3fd7e52c8d25f2fb06fb7005f7cd6de1a989b2',
        '{"acuracia": 0.986, "acuraciaBalanceada": 0.9861, "nTeste": 570, "auc": 1.0}', '2026-11-04', true, now());
COMMIT;
