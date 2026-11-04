"""Treino e inferência veem a mesma imagem (ADR-0001, ação 2).

Grava PNGs sintéticos numa pasta por classe, carrega com
`keras.utils.image_dataset_from_directory` (como o treino fará) e compara, pixel
a pixel, com `preparar_lote` (o que o serviço usa).
"""

import keras
import numpy as np
import pytest
import tensorflow as tf

from dermatoglifia_ia.preprocessamento import (
    AjusteProporcao,
    ImagemInvalida,
    argumentos_dataset,
    decodificar_png,
    preparar_lote,
)


def _png_aleatorio(gerador, altura, largura) -> bytes:
    return tf.io.encode_png(gerador.integers(0, 256, (altura, largura, 1), dtype=np.uint8)).numpy()


@pytest.mark.parametrize('ajuste', list(AjusteProporcao))
def test_mesma_imagem_que_o_image_dataset_from_directory(tmp_path, ajuste):
    gerador = np.random.default_rng(7)
    # Tamanhos e proporções variados, como chegam da coleta (100 a 4000 px na API).
    tamanhos = [(120, 90), (90, 160), (200, 200), (133, 71)]
    pngs = []
    for i, (altura, largura) in enumerate(tamanhos):
        conteudo = _png_aleatorio(gerador, altura, largura)
        pasta = tmp_path / 'VERTICILO'
        pasta.mkdir(exist_ok=True)
        (pasta / f'img{i}.png').write_bytes(conteudo)
        pngs.append(conteudo)

    tamanho = (64, 48)
    dataset = keras.utils.image_dataset_from_directory(
        tmp_path, labels=None, shuffle=False, batch_size=len(pngs), **argumentos_dataset(tamanho, ajuste)
    )
    do_treino = next(iter(dataset)).numpy()
    do_servico = preparar_lote(pngs, tamanho, ajuste)

    assert do_servico.shape == (len(pngs), 64, 48, 1)
    assert do_servico.dtype == np.float32
    np.testing.assert_allclose(do_servico, do_treino, atol=1e-4)


def test_argumentos_dataset_usam_cinza_e_o_ajuste_pedido():
    args = argumentos_dataset([128, 96], AjusteProporcao.PREENCHER)
    assert args == {
        'image_size': (128, 96),
        'color_mode': 'grayscale',
        'interpolation': 'bilinear',
        'crop_to_aspect_ratio': False,
        'pad_to_aspect_ratio': True,
    }


def test_recusa_arquivo_que_nao_e_png():
    with pytest.raises(ImagemInvalida, match='não é PNG'):
        decodificar_png(b'\xff\xd8\xff\xe0 jpeg qualquer')


def test_recusa_png_corrompido():
    with pytest.raises(ImagemInvalida, match='corrompido'):
        decodificar_png(b'\x89PNG\r\n\x1a\n' + b'\x00' * 40)
