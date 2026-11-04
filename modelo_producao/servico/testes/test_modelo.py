import json

import keras
import numpy as np
import pytest
from conftest import CLASSES, TAMANHO, gravar_pasta, modelo_por_brilho

from dermatoglifia_ia.modelo import ErroModelo, OrigemModelo, carregar_pasta, escolher_modelo


def test_carrega_pasta_valida(pasta_modelo):
    modelo = carregar_pasta(pasta_modelo, OrigemModelo.DEMO)
    assert modelo.config.versao == 'teste_sintetico_v1'
    assert [c.value for c in modelo.config.classes] == CLASSES
    probabilidades = modelo.prever(np.full((2, *TAMANHO, 1), 255 * 0.9, dtype=np.float32))
    assert probabilidades.shape == (2, 5)
    assert CLASSES[int(np.argmax(probabilidades[0]))] == CLASSES[4]


def test_recusa_hash_que_nao_confere(tmp_path):
    pasta = gravar_pasta(tmp_path / 'm', modelo_por_brilho())
    (pasta / 'modelo.sha256').write_text('0' * 64, encoding='utf-8')
    with pytest.raises(ErroModelo, match='Hash'):
        carregar_pasta(pasta, OrigemModelo.PRODUCAO)


def test_recusa_classes_incompletas(tmp_path):
    pasta = gravar_pasta(tmp_path / 'm', modelo_por_brilho(), classes=[*CLASSES[:4], 'ARCO'])
    with pytest.raises(ErroModelo, match='5 classes'):
        carregar_pasta(pasta, OrigemModelo.DEMO)


def test_recusa_tamanho_diferente_da_entrada(tmp_path):
    pasta = gravar_pasta(tmp_path / 'm', modelo_por_brilho(), tamanho_imagem=[64, 64])
    with pytest.raises(ErroModelo, match='Entrada do modelo'):
        carregar_pasta(pasta, OrigemModelo.DEMO)


def test_recusa_saida_sem_softmax(tmp_path):
    # A referência das aulas usava sigmoid com 5 classes exclusivas; o ADR-0002 exige softmax.
    pasta = gravar_pasta(tmp_path / 'm', modelo_por_brilho(saida='sigmoid'))
    with pytest.raises(ErroModelo, match='softmax'):
        carregar_pasta(pasta, OrigemModelo.DEMO)


def test_nao_executa_codigo_embutido_no_arquivo(tmp_path):
    entrada = keras.Input((*TAMANHO, 1))
    saida = keras.layers.Lambda(lambda x: x * 0)(entrada)  # código Python dentro do .keras
    saida = keras.layers.Dense(5, activation='softmax')(keras.layers.GlobalAveragePooling2D()(saida))
    pasta = gravar_pasta(tmp_path / 'm', keras.Model(entrada, saida))
    with pytest.raises(ErroModelo, match='Não foi possível carregar'):
        carregar_pasta(pasta, OrigemModelo.DEMO)


def test_config_invalido(tmp_path):
    pasta = gravar_pasta(tmp_path / 'm', modelo_por_brilho())
    (pasta / 'config.json').write_text(json.dumps({'classes': CLASSES}), encoding='utf-8')
    with pytest.raises(ErroModelo, match=r'config\.json'):
        carregar_pasta(pasta, OrigemModelo.DEMO)


def test_escolhe_producao_quando_existe(tmp_path, pasta_modelo):
    producao = gravar_pasta(tmp_path / 'producao', modelo_por_brilho(), versao='real_v1')
    modelo = escolher_modelo(producao, pasta_modelo)
    assert (modelo.origem, modelo.config.versao) == (OrigemModelo.PRODUCAO, 'real_v1')


def test_sem_producao_usa_a_demonstracao(tmp_path, pasta_modelo):
    modelo = escolher_modelo(tmp_path / 'nao-existe', pasta_modelo)
    assert modelo.origem == OrigemModelo.DEMO


def test_producao_invalida_nao_cai_para_a_demonstracao(tmp_path, pasta_modelo):
    producao = tmp_path / 'producao'
    producao.mkdir()
    (producao / 'modelo.keras').write_bytes(b'copiado pela metade')
    with pytest.raises(ErroModelo):
        escolher_modelo(producao, pasta_modelo)


def test_sem_nenhum_modelo(tmp_path):
    with pytest.raises(ErroModelo, match='Nenhum modelo'):
        escolher_modelo(tmp_path / 'a', tmp_path / 'b')
