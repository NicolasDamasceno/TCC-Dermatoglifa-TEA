"""Espelhamento com troca de rótulo (ADR-0002, CLAUDE.md Regra 3).

Espelhar uma digital na horizontal transforma uma alça que abre para a esquerda
numa que abre para a direita. Por isso o espelhamento nunca fica dentro do modelo
(`RandomFlip`): ele só acontece no pipeline de dados, trocando ALCA_ESQUERDA ↔
ALCA_DIREITA junto com a imagem. Arcos, arcos tendidos e verticilos continuam
com o mesmo rótulo.
"""

from __future__ import annotations

from collections.abc import Sequence

import tensorflow as tf

from .dominio import ClasseImagem

TROCA_NO_ESPELHO = {
    ClasseImagem.ALCA_ESQUERDA: ClasseImagem.ALCA_DIREITA,
    ClasseImagem.ALCA_DIREITA: ClasseImagem.ALCA_ESQUERDA,
}


def permutacao_espelho(classes: Sequence[str]) -> list[int]:
    """Para cada posição do vetor one-hot, de qual posição vem o rótulo espelhado."""
    nomes = [ClasseImagem(c) for c in classes]
    return [nomes.index(TROCA_NO_ESPELHO.get(c, c)) for c in nomes]


def espelhar_lote(imagens: tf.Tensor, rotulos: tf.Tensor, permutacao: Sequence[int]) -> tuple[tf.Tensor, tf.Tensor]:
    """Espelha um lote (n, altura, largura, canais) e troca as alças no one-hot."""
    return tf.image.flip_left_right(imagens), tf.gather(rotulos, list(permutacao), axis=-1)


def espelhar_aleatoriamente(dataset: tf.data.Dataset, classes: Sequence[str], semente: int = 42) -> tf.data.Dataset:
    """Espelha cada imagem com probabilidade 1/2, trocando a alça junto (dataset em lotes, rótulos categóricos).

    O sorteio é por imagem, dentro de cada lote: originais e espelhadas se misturam
    em todo lote. Concatenar o conjunto espelhado depois do original fazia cada época
    terminar só com imagens espelhadas, e a rede (e as médias da BatchNormalization)
    oscilava de uma época para a outra.
    """
    permutacao = permutacao_espelho(classes)
    gerador = tf.random.Generator.from_seed(semente)

    def talvez_espelhar(imagens: tf.Tensor, rotulos: tf.Tensor) -> tuple[tf.Tensor, tf.Tensor]:
        sorteio = gerador.uniform([tf.shape(imagens)[0]]) < 0.5
        espelhadas, trocados = espelhar_lote(imagens, rotulos, permutacao)
        return (
            tf.where(sorteio[:, None, None, None], espelhadas, imagens),
            tf.where(sorteio[:, None], trocados, rotulos),
        )

    return dataset.map(talvez_espelhar, num_parallel_calls=tf.data.AUTOTUNE)
