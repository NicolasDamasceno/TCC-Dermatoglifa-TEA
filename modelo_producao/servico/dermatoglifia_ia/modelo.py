"""Carga do modelo da Etapa 1 e de seus metadados.

Formato de uma pasta de modelo (o mesmo que o treino grava em
`treino/modelos/<origem>/<versao>/`, ver treino/README.md):

    modelo.keras    arquitetura e pesos
    modelo.sha256   hash SHA-256 do modelo.keras (só o hash, ou "hash  nome")
    config.json     versao, classes (na ordem da saída), tamanho_imagem e,
                    opcionalmente, ajuste_proporcao, interpolacao, origem_dados
"""

from __future__ import annotations

import hashlib
import json
import threading
from dataclasses import dataclass, field
from enum import StrEnum
from pathlib import Path
from typing import Any

import keras
import numpy as np

from .dominio import ClasseImagem
from .preprocessamento import AjusteProporcao


class ErroModelo(RuntimeError):
    """O modelo não pode ser usado (arquivo ausente, hash ou formato errado)."""


class OrigemModelo(StrEnum):
    PRODUCAO = 'PRODUCAO'  # modelo real, só no servidor da pesquisa (ADR-0005)
    DEMO = 'DEMO'  # modelo treinado só com dados sintéticos, versionado no Git


@dataclass(frozen=True)
class ConfigModelo:
    versao: str
    classes: tuple[ClasseImagem, ...]
    tamanho_imagem: tuple[int, int]
    ajuste_proporcao: AjusteProporcao = AjusteProporcao.ESTICAR
    interpolacao: str = 'bilinear'
    origem_dados: str | None = None

    @classmethod
    def ler(cls, caminho: Path) -> ConfigModelo:
        try:
            dados: dict[str, Any] = json.loads(caminho.read_text(encoding='utf-8'))
            classes = tuple(ClasseImagem(c) for c in dados['classes'])
            altura, largura = (int(v) for v in dados['tamanho_imagem'])
            config = cls(
                versao=str(dados['versao']).strip(),
                classes=classes,
                tamanho_imagem=(altura, largura),
                ajuste_proporcao=AjusteProporcao(dados.get('ajuste_proporcao', AjusteProporcao.ESTICAR)),
                interpolacao=str(dados.get('interpolacao', 'bilinear')),
                origem_dados=dados.get('origem_dados'),
            )
        except (OSError, ValueError, KeyError, TypeError) as erro:
            raise ErroModelo(f'config.json inválido em {caminho.parent}: {erro}') from erro

        if not config.versao:
            raise ErroModelo('config.json sem versão.')
        if sorted(config.classes) != sorted(ClasseImagem):
            raise ErroModelo('config.json: "classes" precisa listar as 5 classes da Etapa 1, cada uma uma vez.')
        if min(config.tamanho_imagem) < 16:
            raise ErroModelo('config.json: tamanho_imagem muito pequeno.')
        return config


def sha256_arquivo(caminho: Path) -> str:
    resumo = hashlib.sha256()
    with caminho.open('rb') as arquivo:
        for bloco in iter(lambda: arquivo.read(1 << 20), b''):
            resumo.update(bloco)
    return resumo.hexdigest()


@dataclass
class ModeloCarregado:
    config: ConfigModelo
    origem: OrigemModelo
    sha256: str
    rede: keras.Model
    _trava: threading.Lock = field(default_factory=threading.Lock, repr=False)

    def prever(self, lote: np.ndarray) -> np.ndarray:
        """Probabilidades (n, 5) na ordem de `config.classes`."""
        with self._trava:  # volume baixo: uma inferência por vez é suficiente e evita disputa
            saida = self.rede(lote, training=False)
        return np.asarray(saida, dtype=np.float64)


def carregar_pasta(pasta: Path, origem: OrigemModelo) -> ModeloCarregado:
    """Lê config, confere o hash e carrega o .keras sem permitir código arbitrário."""
    arquivo = pasta / 'modelo.keras'
    arquivo_hash = pasta / 'modelo.sha256'
    if not arquivo.is_file() or not arquivo_hash.is_file():
        raise ErroModelo(f'{pasta} precisa conter modelo.keras e modelo.sha256.')

    config = ConfigModelo.ler(pasta / 'config.json')
    esperado = arquivo_hash.read_text(encoding='utf-8').split()[0].lower() if arquivo_hash.stat().st_size else ''
    obtido = sha256_arquivo(arquivo)
    if esperado != obtido:
        raise ErroModelo(f'Hash do modelo em {pasta} não confere com modelo.sha256.')

    try:
        # safe_mode impede desserializar lambdas e código Python embutido no arquivo.
        rede = keras.saving.load_model(arquivo, compile=False, safe_mode=True)
    except Exception as erro:
        raise ErroModelo(f'Não foi possível carregar {arquivo}: {erro}') from erro

    entrada = tuple(rede.input_shape)
    if entrada[1:] != (*config.tamanho_imagem, 1):
        raise ErroModelo(
            f'Entrada do modelo {entrada} não corresponde a tamanho_imagem {config.tamanho_imagem} em cinza.'
        )
    if tuple(rede.output_shape)[1:] != (len(ClasseImagem),):
        raise ErroModelo(f'Saída do modelo {rede.output_shape} não tem as 5 classes da Etapa 1.')

    modelo = ModeloCarregado(config=config, origem=origem, sha256=obtido, rede=rede)
    teste = modelo.prever(np.zeros((1, *config.tamanho_imagem, 1), dtype=np.float32))[0]
    if not np.isclose(teste.sum(), 1.0, atol=1e-3) or (teste < 0).any():
        raise ErroModelo('A saída do modelo não é uma distribuição de probabilidades (use softmax, ADR-0002).')
    return modelo


def escolher_modelo(pasta_producao: Path, pasta_demo: Path) -> ModeloCarregado:
    """Modelo de produção se a pasta existir; senão, o de demonstração.

    Se a pasta de produção existe mas o modelo é inválido (hash, formato), o
    serviço NÃO cai para o de demonstração: no servidor da pesquisa, responder
    com um modelo sintético seria pior do que não responder.
    """
    if pasta_producao.is_dir() and any(pasta_producao.iterdir()):
        return carregar_pasta(pasta_producao, OrigemModelo.PRODUCAO)
    if pasta_demo.is_dir() and (pasta_demo / 'modelo.keras').exists():
        return carregar_pasta(pasta_demo, OrigemModelo.DEMO)
    raise ErroModelo(
        'Nenhum modelo disponível: falta o modelo de demonstração em modelo_producao/demo/ '
        '(gerado pelo script de treino com dados sintéticos).'
    )
