import json
import sys
from pathlib import Path

import etapa1
import keras
import numpy as np
import pytest
import sinteticos
from dermatoglifia_ia.modelo import OrigemModelo, carregar_pasta


def test_cnn_segue_a_referencia_com_os_ajustes_do_adr_0002():
    modelo = etapa1.construir_cnn((64, 64), blocos=3)
    tipos = [type(c).__name__ for c in modelo.layers]
    aumento = modelo.get_layer('augmentation')

    assert modelo.input_shape == (None, 64, 64, 1)  # escala de cinza
    assert 'RandomFlip' not in [type(c).__name__ for c in aumento.layers]  # espelhar troca a alça
    assert aumento.layers[0].factor == pytest.approx((-20 / 360, 20 / 360))  # rotação de até ±20°
    assert tipos.count('Conv2D') == 3 and tipos.count('BatchNormalization') == 3
    assert 'GlobalAveragePooling2D' in tipos
    saida = modelo.layers[-1]
    assert saida.units == 5 and saida.activation is keras.activations.softmax  # não sigmoid


def test_avaliar_calcula_sensibilidade_e_especificidade():
    # 4 verticilos (3 acertos) e 4 arcos (todos certos)
    reais = np.array([4, 4, 4, 4, 2, 2, 2, 2])
    probabilidades = np.full((8, 5), 0.05)
    for i, previsto in enumerate([4, 4, 4, 2, 2, 2, 2, 2]):
        probabilidades[i, previsto] = 0.8

    m = etapa1.avaliar(reais, probabilidades)

    assert m['acuracia'] == pytest.approx(7 / 8)
    assert m['por_classe']['VERTICILO']['sensibilidade'] == pytest.approx(3 / 4)
    assert m['por_classe']['VERTICILO']['especificidade'] == pytest.approx(1.0)
    assert m['por_classe']['ARCO']['especificidade'] == pytest.approx(3 / 4)
    assert m['por_classe']['ALCA_DIREITA']['sensibilidade'] is None  # classe ausente no teste
    assert m['matriz_confusao']['valores'][4][2] == 1


def test_sql_de_registro_ativa_so_este_modelo():
    m = etapa1.avaliar(np.array([0, 1]), np.eye(5)[[0, 1]])
    sql = etapa1.sql_registro(
        '2026-11-04_padroes_sintetico_v1', 'SINTETICO', 'a' * 64, m, etapa1.date(2026, 11, 4), 'CNN'
    )
    assert 'UPDATE modelo_ia SET ativo = false WHERE ativo;' in sql
    assert "'2026-11-04_padroes_sintetico_v1'" in sql and 'true, now());' in sql


@pytest.mark.parametrize('versao', ["x'; DROP TABLE modelo_ia; --", 'a b', ''])
def test_versao_so_aceita_caracteres_seguros(versao):
    assert not etapa1.VERSAO_VALIDA.match(versao)


def test_treino_curto_gera_pasta_que_o_servico_carrega(tmp_path, monkeypatch):
    dados = tmp_path / 'dados'
    sinteticos.gerar_conjunto(dados, participantes=14, semente=3, processos=2, lado=64)
    modelos, demo = tmp_path / 'modelos', tmp_path / 'demo'
    monkeypatch.setattr(
        sys,
        'argv',
        [
            'etapa1.py',
            '--dados',
            str(dados),
            '--origem',
            'SINTETICO',
            '--versao',
            'teste_v1',
            '--tamanho',
            '32',
            '--blocos',
            '2',
            '--filtros',
            '8',
            '--epocas',
            '1',
            '--lote',
            '16',
            '--destino-modelos',
            str(modelos),
            '--promover-demo',
            '--pasta-demo',
            str(demo),
        ],
    )

    pasta = etapa1.principal()

    assert pasta == modelos / 'sinteticos' / 'teste_v1'
    arquivos = {p.name for p in pasta.iterdir()}
    assert {'modelo.keras', 'modelo.sha256', 'config.json', 'metricas.json', 'registro.sql', 'NOTAS.md'} <= arquivos
    assert {p.name for p in (pasta / 'graficos').iterdir()} == {'curvas.svg', 'matriz_confusao.svg'}
    metricas = json.loads((pasta / 'metricas.json').read_text())
    assert set(metricas['teste']['por_classe']) == set(etapa1.CLASSES)
    # imagens de digitais nunca vão para a pasta versionada
    assert not list(pasta.rglob('*.png'))
    assert (dados / 'saidas' / 'teste_v1' / 'previsoes.png').exists()
    # o serviço carrega exatamente o que o treino gravou
    modelo = carregar_pasta(demo, OrigemModelo.DEMO)
    assert modelo.config.versao == 'teste_v1'
    assert modelo.config.tamanho_imagem == (32, 32)


def test_modelo_real_exige_pesos_fora_do_repositorio(tmp_path, monkeypatch):
    monkeypatch.setattr(sys, 'argv', ['etapa1.py', '--dados', str(tmp_path), '--origem', 'REAL'])
    with pytest.raises(SystemExit, match='FORA do repositório'):
        etapa1.principal()
    monkeypatch.setattr(
        sys,
        'argv',
        [
            'etapa1.py',
            '--dados',
            str(tmp_path),
            '--origem',
            'REAL',
            '--pesos-reais',
            str(etapa1.RAIZ_REPOSITORIO / 'x'),
        ],
    )
    with pytest.raises(SystemExit, match='FORA do repositório'):
        etapa1.principal()


def test_dados_dentro_do_repositorio_sao_recusados(monkeypatch):
    destino = Path(etapa1.RAIZ_REPOSITORIO) / 'treino' / 'dados'
    monkeypatch.setattr(sys, 'argv', ['etapa1.py', '--dados', str(destino), '--origem', 'SINTETICO'])
    with pytest.raises(SystemExit, match='fora do repositório'):
        etapa1.principal()
