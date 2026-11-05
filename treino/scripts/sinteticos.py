"""Gerador de digitais SINTÉTICAS para o modelo de demonstração (ADR-0005, ADR-0008).

Nenhuma imagem aqui vem de uma pessoa. Cada digital nasce de um campo de
orientação das cristas e de um ruído aleatório:

1. Campo de orientação por classe. Alças, verticilo e arco tendido usam o modelo
   zero-polo de Sherlock & Monro (1993): núcleos e deltas posicionados no plano,
   θ(z) = ½·Σ arg(z − núcleo) − ½·Σ arg(z − delta). O arco simples, que não tem
   núcleo nem delta, usa cristas como curvas de nível de uma "lombada" suave.
2. Cristas. Ruído aleatório filtrado repetidas vezes por filtros de Gabor
   alinhados ao campo, como no SFinGe (Cappelli et al., ICPR 2000).
3. Acabamento. Contorno elíptico do dedo, intensidade da tinta e ruído variáveis.

Convenção (a mesma da tabela de conversão do back): ALCA_DIREITA é a alça cujas
cristas entram e saem pela direita da imagem, com o delta à esquerda.

As imagens são simplificadas: servem para o sistema funcionar de ponta a ponta e
para testar o treino, não para representar a variedade de digitais reais (domain
shift, doc 07). O modelo treinado com elas não deve ser interpretado.
"""

from __future__ import annotations

import argparse
import csv
import os
from concurrent.futures import ProcessPoolExecutor
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image

CLASSES = ('ARCO', 'ARCO_TENDIDO', 'ALCA_ESQUERDA', 'ALCA_DIREITA', 'VERTICILO')
MAOS = ('DIREITA', 'ESQUERDA')
DEDOS = ('POLEGAR', 'INDICADOR', 'MEDIO', 'ANELAR', 'MINIMO')
LADO = 320  # pixels; gravadas com 500 ppi, como pede o ADR-0004
PPI = 500


def _grade(lado: int) -> tuple[np.ndarray, np.ndarray]:
    yy, xx = np.mgrid[0:lado, 0:lado].astype(np.float64)
    return xx - lado / 2, lado / 2 - yy  # x para a direita, y para cima, origem no centro


def campo_zero_polo(lado: int, nucleos: list[complex], deltas: list[complex], rotacao: float) -> np.ndarray:
    """Orientação das cristas (radianos, módulo π) pelo modelo de Sherlock & Monro."""
    x, y = _grade(lado)
    z = x + 1j * y
    theta = np.zeros_like(x)
    for n in nucleos:
        theta += 0.5 * np.angle(z - n)
    for d in deltas:
        theta -= 0.5 * np.angle(z - d)
    return theta + rotacao


def campo_arco(lado: int, altura: float, largura: float, rotacao: float) -> np.ndarray:
    """Arco simples: cristas são curvas de nível de f = y − altura·g(y)·exp(−(x/largura)²)."""
    x, y = _grade(lado)
    g = 1 / (1 + np.exp(-(y + 40) / 30))  # curvatura cresce para cima, como no dedo
    e = np.exp(-((x / largura) ** 2))
    dfdx = altura * g * e * 2 * x / largura**2
    dfdy = 1 - altura * e * g * (1 - g) / 30
    return np.arctan2(dfdy, dfdx) - np.pi / 2 + rotacao  # a crista é perpendicular ao gradiente


def _nucleos_gabor(lado: int, periodo: float, faixas: int) -> list[np.ndarray]:
    raio = min(25, lado // 2 - 1)
    ky, kx = np.mgrid[-raio : raio + 1, -raio : raio + 1].astype(np.float64)
    sigma = periodo * 0.45
    nucleos = []
    for b in range(faixas):
        angulo = (b + 0.5) / faixas * np.pi  # direção da crista
        nx, ny = -np.sin(angulo), np.cos(angulo)  # normal à crista
        projecao = kx * nx - ky * ny
        g = np.exp(-(kx**2 + ky**2) / (2 * sigma**2)) * np.cos(2 * np.pi * projecao / periodo)
        g -= g.mean()
        k = np.zeros((lado, lado))
        k[: 2 * raio + 1, : 2 * raio + 1] = g
        nucleos.append(np.fft.rfft2(np.roll(k, (-raio, -raio), axis=(0, 1))))
    return nucleos


def renderizar_cristas(
    theta: np.ndarray, periodo: float, rng: np.random.Generator, iteracoes: int = 10, faixas: int = 16
) -> np.ndarray:
    """Cristas em [−1, 1] seguindo o campo, por filtragem de Gabor iterada."""
    lado = theta.shape[0]
    faixa = np.floor(np.mod(theta, np.pi) / np.pi * faixas).astype(int) % faixas
    nucleos = _nucleos_gabor(lado, periodo, faixas)
    imagem = rng.standard_normal((lado, lado))
    for _ in range(iteracoes):
        espectro = np.fft.rfft2(imagem)
        saida = np.zeros_like(imagem)
        for b, nucleo in enumerate(nucleos):
            mascara = faixa == b
            if mascara.any():
                saida[mascara] = np.fft.irfft2(espectro * nucleo, s=(lado, lado))[mascara]
        imagem = np.tanh(2 * saida / (saida.std() + 1e-9))
    return imagem


def campo_da_classe(classe: str, rng: np.random.Generator, lado: int = LADO) -> np.ndarray:
    rotacao = np.deg2rad(rng.uniform(-12, 12))
    if classe == 'ARCO':
        return campo_arco(lado, rng.uniform(40, 75), rng.uniform(70, 110), rotacao)
    nucleo = complex(rng.uniform(-15, 15), rng.uniform(0, 35))
    if classe == 'ARCO_TENDIDO':
        # núcleo logo acima do delta, na mesma vertical
        delta = nucleo - 1j * rng.uniform(30, 55) + rng.uniform(-6, 6)
        return campo_zero_polo(lado, [nucleo], [delta], rotacao)
    if classe in ('ALCA_ESQUERDA', 'ALCA_DIREITA'):
        lado_delta = 1 if classe == 'ALCA_ESQUERDA' else -1  # o delta fica do lado oposto à abertura
        delta = nucleo + complex(lado_delta * rng.uniform(70, 110), -rng.uniform(70, 110))
        return campo_zero_polo(lado, [nucleo], [delta], rotacao)
    if classe == 'VERTICILO':
        # Dois núcleos com eixo sorteado: a espiral gira para um lado ou para o outro,
        # como nos verticilos reais. Com o eixo sempre horizontal, todos girariam igual
        # e o modelo confundia verticilo com alça de um lado só.
        meia = rng.uniform(3, 14) * np.exp(1j * rng.uniform(0, np.pi))
        deltas = [
            nucleo + complex(-rng.uniform(75, 110), -rng.uniform(65, 105)),
            nucleo + complex(rng.uniform(75, 110), -rng.uniform(65, 105)),
        ]
        return campo_zero_polo(lado, [nucleo - meia, nucleo + meia], deltas, rotacao)
    raise ValueError(f'Classe desconhecida: {classe}')


def gerar_digital(classe: str, rng: np.random.Generator, lado: int = LADO) -> np.ndarray:
    """Uma digital sintética em cinza 8 bits (cristas escuras sobre fundo claro)."""
    cristas = renderizar_cristas(campo_da_classe(classe, rng, lado), rng.uniform(7.5, 10.5), rng)
    x, y = _grade(lado)
    a, b = rng.uniform(0.34, 0.42) * lado, rng.uniform(0.42, 0.49) * lado
    deslocamento = complex(rng.uniform(-12, 12), rng.uniform(-12, 12))
    contorno = np.clip((1 - ((x - deslocamento.real) / a) ** 2 - ((y - deslocamento.imag) / b) ** 2) / 0.15, 0, 1)
    tinta = (0.5 + 0.5 * cristas) * rng.uniform(150, 230) * contorno
    valores = 255 - tinta + rng.normal(0, rng.uniform(3, 10), tinta.shape)
    return np.clip(valores, 0, 255).astype(np.uint8)


@dataclass(frozen=True)
class Tarefa:
    participante: str
    mao: str
    dedo: str
    classe: str
    semente: int
    destino: Path
    lado: int = LADO


def _executar(tarefa: Tarefa) -> None:
    imagem = gerar_digital(tarefa.classe, np.random.default_rng(tarefa.semente), tarefa.lado)
    Image.fromarray(imagem).save(tarefa.destino, dpi=(PPI, PPI))


def gerar_conjunto(
    saida: Path, participantes: int, semente: int = 42, processos: int | None = None, lado: int = LADO
) -> Path:
    """Gera `participantes` × 10 dedos e um rotulos.csv no formato que o treino espera.

    Como numa coleta real, cada participante tem os 10 dedos; a classe de cada dedo é
    sorteada de modo uniforme (classes equilibradas). O arquivo de rótulos tem as
    colunas arquivo, participante, mao, dedo, classe.
    """
    imagens = saida / 'imagens'
    imagens.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(semente)
    tarefas = []
    for p in range(participantes):
        codigo = f'S{p:05d}'  # código sintético, sem relação com pessoa alguma
        for mao in MAOS:
            for dedo in DEDOS:
                tarefas.append(
                    Tarefa(
                        codigo,
                        mao,
                        dedo,
                        str(rng.choice(CLASSES)),
                        int(rng.integers(2**32)),
                        imagens / f'{codigo}_{mao}_{dedo}.png',
                        lado,
                    )
                )

    with ProcessPoolExecutor(max_workers=processos or os.cpu_count()) as executor:
        list(executor.map(_executar, tarefas, chunksize=16))

    rotulos = saida / 'rotulos.csv'
    with rotulos.open('w', newline='', encoding='utf-8') as arquivo:
        escritor = csv.writer(arquivo)
        escritor.writerow(['arquivo', 'participante', 'mao', 'dedo', 'classe'])
        for t in tarefas:
            escritor.writerow([f'imagens/{t.destino.name}', t.participante, t.mao, t.dedo, t.classe])
    return rotulos


def principal() -> None:
    parser = argparse.ArgumentParser(description='Gera digitais sintéticas rotuladas para o treino da Etapa 1.')
    parser.add_argument(
        '--saida', type=Path, default=Path(os.environ.get('TCC_DADOS_DIR', 'dados_locais')) / 'sinteticos'
    )
    parser.add_argument('--participantes', type=int, default=400)
    parser.add_argument('--semente', type=int, default=42)
    parser.add_argument('--processos', type=int, default=None)
    args = parser.parse_args()
    rotulos = gerar_conjunto(args.saida, args.participantes, args.semente, args.processos)
    print(f'{args.participantes * 10} imagens sintéticas em {args.saida}; rótulos em {rotulos}')


if __name__ == '__main__':
    principal()
