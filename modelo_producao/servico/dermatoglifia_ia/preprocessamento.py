"""Pré-processamento das imagens: o mesmo no treino e na inferência (ADR-0001).

O treino carrega as pastas com `keras.utils.image_dataset_from_directory`
(Regra 3 do CLAUDE.md). Com o backend TensorFlow, essa função decodifica cada
arquivo com `tf.image.decode_image` e redimensiona com `tf.image.resize`
(ou `smart_resize` / `resize_with_pad`, conforme o ajuste de proporção). As funções
abaixo repetem exatamente essas chamadas, e `argumentos_dataset` entrega ao treino
os mesmos parâmetros, tirados do mesmo `config.json`. O teste
`test_preprocessamento.py` confere, pixel a pixel, que os dois caminhos dão o mesmo
resultado.

O `Rescaling(1./255)` fica dentro do modelo (estrutura do modelo_arroz); por isso
as imagens saem daqui em float32 de 0 a 255.
"""

from __future__ import annotations

from collections.abc import Sequence
from enum import StrEnum
from typing import Any

import keras
import numpy as np
import tensorflow as tf

ASSINATURA_PNG = b'\x89PNG\r\n\x1a\n'


class AjusteProporcao(StrEnum):
    """Como levar a imagem ao tamanho do modelo (parâmetros do Keras)."""

    ESTICAR = 'ESTICAR'  # padrão do Keras: redimensiona sem manter a proporção
    PREENCHER = 'PREENCHER'  # pad_to_aspect_ratio=True: mantém a proporção com bordas
    RECORTAR = 'RECORTAR'  # crop_to_aspect_ratio=True: mantém a proporção cortando o excesso


class ImagemInvalida(ValueError):
    """O arquivo não é uma imagem que o modelo possa receber."""


def argumentos_dataset(
    tamanho: Sequence[int], ajuste: AjusteProporcao = AjusteProporcao.ESTICAR, interpolacao: str = 'bilinear'
) -> dict[str, Any]:
    """Parâmetros de `image_dataset_from_directory` que definem o que o modelo vê."""
    return {
        'image_size': (int(tamanho[0]), int(tamanho[1])),
        'color_mode': 'grayscale',
        'interpolation': interpolacao,
        'crop_to_aspect_ratio': ajuste is AjusteProporcao.RECORTAR,
        'pad_to_aspect_ratio': ajuste is AjusteProporcao.PREENCHER,
    }


def decodificar_png(conteudo: bytes) -> tf.Tensor:
    """PNG → tensor uint8 (altura, largura, 1). A API já envia PNG cinza de 8 bits."""
    if not conteudo.startswith(ASSINATURA_PNG):
        raise ImagemInvalida('O arquivo não é PNG.')
    try:
        return tf.image.decode_image(conteudo, channels=1, expand_animations=False)
    except (tf.errors.InvalidArgumentError, ValueError) as erro:
        raise ImagemInvalida('PNG corrompido ou ilegível.') from erro


def redimensionar(
    imagem: tf.Tensor,
    tamanho: Sequence[int],
    ajuste: AjusteProporcao = AjusteProporcao.ESTICAR,
    interpolacao: str = 'bilinear',
) -> tf.Tensor:
    """As mesmas chamadas de `keras.src.utils.image_dataset_utils._load_image_tf`."""
    alvo = (int(tamanho[0]), int(tamanho[1]))
    if ajuste is AjusteProporcao.RECORTAR:
        return keras.preprocessing.image.smart_resize(
            imagem, alvo, interpolation=interpolacao, data_format='channels_last'
        )
    if ajuste is AjusteProporcao.PREENCHER:
        return tf.image.resize_with_pad(imagem, alvo[0], alvo[1], method=interpolacao)
    return tf.image.resize(imagem, alvo, method=interpolacao)


def preparar_lote(
    imagens: Sequence[bytes],
    tamanho: Sequence[int],
    ajuste: AjusteProporcao = AjusteProporcao.ESTICAR,
    interpolacao: str = 'bilinear',
) -> np.ndarray:
    """Lista de PNGs → lote float32 (n, altura, largura, 1), pronto para o modelo."""
    tensores = [redimensionar(decodificar_png(c), tamanho, ajuste, interpolacao) for c in imagens]
    return np.stack([tf.cast(t, tf.float32).numpy() for t in tensores])
