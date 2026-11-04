"""Fixtures com modelos e imagens SINTÉTICOS (CLAUDE.md, Regra 4): nada aqui é digital real."""

import os

os.environ.setdefault('TF_CPP_MIN_LOG_LEVEL', '3')

import json
from pathlib import Path

import keras
import numpy as np
import pytest
import tensorflow as tf

from dermatoglifia_ia.dominio import ClasseImagem
from dermatoglifia_ia.modelo import sha256_arquivo

# Ordem alfabética: é a que o image_dataset_from_directory dá às pastas de classe.
CLASSES = sorted(c.value for c in ClasseImagem)
TAMANHO = (32, 32)
# Brilho médio que faz o modelo de teste escolher cada classe (ver modelo_por_brilho).
CENTROS = (0.1, 0.3, 0.5, 0.7, 0.9)


def modelo_por_brilho(tamanho=TAMANHO, saida='softmax') -> keras.Model:
    """CNN mínima em que o brilho médio decide a classe, com pesos montados à mão.

    logit_i = 2·K·c_i·m − K·c_i², que tem o mesmo argmax de −K·(m − c_i)²: vence a
    classe cujo centro c_i está mais perto do brilho médio m (de 0 a 1).
    """
    entrada = keras.Input((*tamanho, 1))
    x = keras.layers.Rescaling(1.0 / 255)(entrada)
    x = keras.layers.GlobalAveragePooling2D()(x)
    densa = keras.layers.Dense(5, activation=saida)
    modelo = keras.Model(entrada, densa(x))
    k = 400.0
    centros = np.array(CENTROS)
    densa.set_weights([np.array([2 * k * centros]), -k * centros**2])
    return modelo


def gravar_pasta(pasta: Path, modelo: keras.Model, **config) -> Path:
    pasta.mkdir(parents=True, exist_ok=True)
    modelo.save(pasta / 'modelo.keras')
    (pasta / 'modelo.sha256').write_text(f'{sha256_arquivo(pasta / "modelo.keras")}  modelo.keras\n', encoding='utf-8')
    dados = {
        'versao': 'teste_sintetico_v1',
        'classes': CLASSES,
        'tamanho_imagem': list(TAMANHO),
        'origem_dados': 'SINTETICO',
    }
    dados.update(config)
    (pasta / 'config.json').write_text(json.dumps(dados), encoding='utf-8')
    return pasta


@pytest.fixture(scope='session')
def pasta_modelo(tmp_path_factory) -> Path:
    return gravar_pasta(tmp_path_factory.mktemp('modelo') / 'demo', modelo_por_brilho())


def png_listrado(brilho: float, altura=60, largura=48, amplitude=30) -> bytes:
    """Imagem cinza com listras (como cristas), de brilho médio `brilho` (0 a 1)."""
    linhas = np.arange(altura)[:, None] * np.ones((1, largura))
    valores = brilho * 255 + amplitude * np.sin(linhas / 2.0)
    imagem = np.clip(np.round(valores), 0, 255).astype(np.uint8)[..., None]
    return tf.io.encode_png(imagem).numpy()


def png_uniforme(valor=200, altura=40, largura=40) -> bytes:
    return tf.io.encode_png(np.full((altura, largura, 1), valor, dtype=np.uint8)).numpy()


def brilho_da_classe(classe: str) -> float:
    return CENTROS[CLASSES.index(classe)]
