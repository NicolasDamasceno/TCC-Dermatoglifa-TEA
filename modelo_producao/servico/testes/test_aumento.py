import numpy as np
import tensorflow as tf
from conftest import CLASSES

from dermatoglifia_ia.aumento import espelhar_aleatoriamente, espelhar_lote, permutacao_espelho


def _um_quente(classe):
    v = np.zeros(len(CLASSES), dtype=np.float32)
    v[CLASSES.index(classe)] = 1
    return v


def test_espelho_troca_so_as_alcas():
    permutacao = permutacao_espelho(CLASSES)
    trocados = {CLASSES[i]: CLASSES[j] for i, j in enumerate(permutacao) if i != j}
    assert trocados == {'ALCA_DIREITA': 'ALCA_ESQUERDA', 'ALCA_ESQUERDA': 'ALCA_DIREITA'}


def test_espelhar_lote_inverte_a_imagem_e_o_rotulo_da_alca():
    imagem = np.zeros((1, 4, 4, 1), dtype=np.float32)
    imagem[0, :, 0, 0] = 255  # coluna da esquerda acesa
    rotulos = np.stack([_um_quente('ALCA_ESQUERDA')])

    espelhada, novos = espelhar_lote(tf.constant(imagem), tf.constant(rotulos), permutacao_espelho(CLASSES))

    assert espelhada.numpy()[0, :, 3, 0].tolist() == [255] * 4  # foi para a direita
    assert CLASSES[int(np.argmax(novos.numpy()[0]))] == 'ALCA_DIREITA'


def test_verticilo_continua_verticilo_no_espelho():
    _, novos = espelhar_lote(
        tf.zeros((1, 4, 4, 1)), tf.constant([_um_quente('VERTICILO')]), permutacao_espelho(CLASSES)
    )
    assert CLASSES[int(np.argmax(novos.numpy()[0]))] == 'VERTICILO'


def test_espelhamento_aleatorio_mistura_e_mantem_imagem_e_rotulo_coerentes():
    # Cada imagem tem a coluna da esquerda acesa e é uma alça esquerda: se foi espelhada,
    # a coluna acesa vai para a direita e o rótulo vira alça direita, sempre juntos.
    n = 200
    imagens = np.zeros((n, 4, 4, 1), np.float32)
    imagens[:, :, 0, 0] = 255
    rotulos = np.stack([_um_quente('ALCA_ESQUERDA')] * n)
    dados = tf.data.Dataset.from_tensor_slices((imagens, rotulos)).batch(50)

    espelhadas = 0
    for x, y in espelhar_aleatoriamente(dados, CLASSES, semente=1):
        for imagem, rotulo in zip(x.numpy(), y.numpy(), strict=True):
            virou = imagem[0, 3, 0] == 255
            assert CLASSES[int(np.argmax(rotulo))] == ('ALCA_DIREITA' if virou else 'ALCA_ESQUERDA')
            espelhadas += int(virou)
    assert 60 < espelhadas < 140  # cerca de metade, misturadas em todos os lotes
