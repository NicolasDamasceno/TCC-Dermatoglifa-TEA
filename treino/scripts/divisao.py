"""Divisão treino/validação/teste POR PARTICIPANTE (CLAUDE.md Regra 3; doc 07).

Os 10 dedos de uma pessoa ficam sempre no mesmo conjunto: se dedos da mesma
pessoa caíssem em treino e teste, a avaliação seria otimista (vazamento). A
divisão usa `StratifiedGroupKFold` do scikit-learn, com o participante como
grupo, o que também mantém a proporção das classes.

Depois monta a estrutura que o `image_dataset_from_directory` espera (uma pasta
por classe), com links para as imagens originais, como a `montar_estrutura_virtual`
do modelo_arroz. Nada é copiado para dentro do repositório: tudo fica na pasta de
dados local.
"""

from __future__ import annotations

import csv
import json
import os
import shutil
from collections import Counter
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from sklearn.model_selection import StratifiedGroupKFold

CONJUNTOS = ('treino', 'validacao', 'teste')


@dataclass(frozen=True)
class Exemplo:
    arquivo: Path
    participante: str
    classe: str


def ler_rotulos(caminho: Path) -> list[Exemplo]:
    """CSV com as colunas arquivo (relativo ao CSV), participante e classe."""
    with caminho.open(encoding='utf-8') as arquivo:
        linhas = list(csv.DictReader(arquivo))
    faltando = {'arquivo', 'participante', 'classe'} - set(linhas[0] if linhas else {})
    if faltando:
        raise ValueError(f'{caminho} precisa das colunas: {", ".join(sorted(faltando))}.')
    return [Exemplo(caminho.parent / x['arquivo'], x['participante'].strip(), x['classe'].strip()) for x in linhas]


def dividir(
    exemplos: list[Exemplo], fracao_validacao: float = 0.15, fracao_teste: float = 0.15, semente: int = 42
) -> dict[str, list[Exemplo]]:
    """Separa por participante, mantendo a proporção de classes o quanto possível."""
    classes = np.array([e.classe for e in exemplos])
    grupos = np.array([e.participante for e in exemplos])

    def separar(indices: np.ndarray, fracao: float) -> tuple[np.ndarray, np.ndarray]:
        dobras = max(2, round(1 / fracao))
        divisor = StratifiedGroupKFold(n_splits=dobras, shuffle=True, random_state=semente)
        resto, separado = next(divisor.split(indices, classes[indices], grupos[indices]))
        return indices[resto], indices[separado]

    todos = np.arange(len(exemplos))
    resto, teste = separar(todos, fracao_teste)
    treino, validacao = separar(resto, fracao_validacao / (1 - fracao_teste))
    divisao = {
        'treino': [exemplos[i] for i in treino],
        'validacao': [exemplos[i] for i in validacao],
        'teste': [exemplos[i] for i in teste],
    }

    participantes = {nome: {e.participante for e in lista} for nome, lista in divisao.items()}
    for a in CONJUNTOS:
        for b in CONJUNTOS:
            if a < b and participantes[a] & participantes[b]:
                raise AssertionError(f'Participante em {a} e {b}: a divisão vazou.')
    return divisao


def resumo(divisao: dict[str, list[Exemplo]]) -> dict[str, dict]:
    """Contagens por conjunto e classe, sem nenhum código de participante (vai para o Git)."""
    return {
        nome: {
            'imagens': len(lista),
            'participantes': len({e.participante for e in lista}),
            'por_classe': dict(sorted(Counter(e.classe for e in lista).items())),
        }
        for nome, lista in divisao.items()
    }


def _ligar(origem: Path, destino: Path) -> None:
    try:
        os.link(origem, destino)  # link físico: sem cópia e sem permissão especial no Windows
    except OSError:
        try:
            os.symlink(origem.resolve(), destino)
        except OSError:
            shutil.copy2(origem, destino)


def montar_estrutura_virtual(divisao: dict[str, list[Exemplo]], raiz: Path) -> dict[str, Path]:
    """raiz/<conjunto>/<CLASSE>/<arquivo>: o formato do image_dataset_from_directory."""
    if raiz.exists():
        shutil.rmtree(raiz)
    pastas = {}
    for nome, lista in divisao.items():
        for exemplo in lista:
            destino = raiz / nome / exemplo.classe / exemplo.arquivo.name
            destino.parent.mkdir(parents=True, exist_ok=True)
            _ligar(exemplo.arquivo, destino)
        pastas[nome] = raiz / nome
    return pastas


def gravar_divisao(divisao: dict[str, list[Exemplo]], caminho: Path) -> None:
    """Quem ficou em cada conjunto. Fica SÓ na pasta de dados local (tem códigos de participante)."""
    conteudo = {nome: sorted({e.participante for e in lista}) for nome, lista in divisao.items()}
    caminho.write_text(json.dumps(conteudo, indent=1, ensure_ascii=False), encoding='utf-8')
