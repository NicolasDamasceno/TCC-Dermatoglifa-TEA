"""Participantes SINTÉTICOS para conferir o código da Etapa 2 (ADR-0008, ADR-0011).

Nenhuma linha aqui vem de uma pessoa, e o grupo TEA/controle é INVENTADO. O
gerador existe para testar o `etapa2.py` de ponta a ponta: com efeito zero, o
modelo não deve achar nada; com efeito, deve achar. As métricas obtidas com
estes dados não dizem nada sobre TEA.

Como cada participante nasce:

1. Grupo, sexo e idade sorteados sem relação entre si (`--fracao-tea`,
   `--fracao-feminina`, idade uniforme de 24 a 144 meses).
2. Padrão de cada dedo da mão direita sorteado de modo uniforme entre os cinco.
   Não são as frequências de nenhuma população: só um ponto de partida neutro.
3. Cada dedo da mão esquerda repete o homólogo da direita, exceto com
   probabilidade `--discordancia-controle` (ou `--discordancia-tea`), quando
   recebe um dos outros quatro padrões. A discordância bilateral sem polegar é,
   então, Binomial(4, p) por participante.

Os valores padrão de p (0,45 e 0,56) reproduzem as MÉDIAS de pares discordantes
de de Bruin et al. (2014), 1,80 e 2,24 em 4 pares (doc 07). Servem para ver o
tamanho do sinal que o protocolo consegue medir com o efeito publicado, não para
estimá-lo.
"""

from __future__ import annotations

import argparse
import csv
import os
from pathlib import Path

import numpy as np
from dermatoglifia_ia.dominio import ORDEM_COLETA, Dedo, Mao, Padrao, Posicao

PADROES = tuple(Padrao)
DISCORDANCIA_CONTROLE = 1.80 / 4  # de Bruin et al. 2014, média do grupo controle
DISCORDANCIA_TEA = 2.24 / 4  # de Bruin et al. 2014, média do grupo TEA
IDADE_MESES = (24, 144)


def gerar_participante(rng: np.random.Generator, discordancia: float) -> dict[Posicao, Padrao]:
    padroes: dict[Posicao, Padrao] = {}
    for dedo in Dedo:
        direita = PADROES[rng.integers(len(PADROES))]
        if rng.random() < discordancia:
            outros = [p for p in PADROES if p is not direita]
            esquerda = outros[rng.integers(len(outros))]
        else:
            esquerda = direita
        padroes[Posicao(Mao.DIREITA, dedo)] = direita
        padroes[Posicao(Mao.ESQUERDA, dedo)] = esquerda
    return padroes


def gerar_conjunto(
    saida: Path,
    participantes: int,
    semente: int = 42,
    fracao_tea: float = 0.5,
    fracao_feminina: float = 0.2,
    discordancia_controle: float = DISCORDANCIA_CONTROLE,
    discordancia_tea: float = DISCORDANCIA_TEA,
) -> tuple[Path, Path]:
    """Grava participantes.csv e padroes.csv no formato que o etapa2.py lê."""
    saida.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(semente)
    arquivo_participantes, arquivo_padroes = saida / 'participantes.csv', saida / 'padroes.csv'
    with (
        arquivo_participantes.open('w', newline='', encoding='utf-8') as f_part,
        arquivo_padroes.open('w', newline='', encoding='utf-8') as f_pad,
    ):
        e_part, e_pad = csv.writer(f_part), csv.writer(f_pad)
        e_part.writerow(['participante', 'sexo', 'idade_meses', 'grupo'])
        e_pad.writerow(['participante', 'mao', 'dedo', 'padrao'])
        for i in range(participantes):
            codigo = f'E{i:05d}'  # código sintético, sem relação com pessoa alguma
            tea = rng.random() < fracao_tea
            sexo = 'FEMININO' if rng.random() < fracao_feminina else 'MASCULINO'
            idade = int(rng.integers(IDADE_MESES[0], IDADE_MESES[1] + 1))
            e_part.writerow([codigo, sexo, idade, 'TEA' if tea else 'CONTROLE'])
            padroes = gerar_participante(rng, discordancia_tea if tea else discordancia_controle)
            for posicao in ORDEM_COLETA:
                e_pad.writerow([codigo, posicao.mao, posicao.dedo, padroes[posicao]])
    return arquivo_participantes, arquivo_padroes


def principal() -> None:
    p = argparse.ArgumentParser(description='Gera participantes sintéticos para conferir a Etapa 2.')
    p.add_argument(
        '--saida', type=Path, default=Path(os.environ.get('TCC_DADOS_DIR', 'dados_locais')) / 'sinteticos_etapa2'
    )
    p.add_argument('--participantes', type=int, default=300)
    p.add_argument('--semente', type=int, default=42)
    p.add_argument('--fracao-tea', type=float, default=0.5)
    p.add_argument('--fracao-feminina', type=float, default=0.2)
    p.add_argument('--discordancia-controle', type=float, default=DISCORDANCIA_CONTROLE)
    p.add_argument('--discordancia-tea', type=float, default=DISCORDANCIA_TEA)
    args = p.parse_args()
    for nome in ('fracao_tea', 'fracao_feminina', 'discordancia_controle', 'discordancia_tea'):
        if not 0 <= getattr(args, nome) <= 1:
            p.error(f'--{nome.replace("_", "-")} deve ficar entre 0 e 1.')
    caminhos = gerar_conjunto(
        args.saida,
        args.participantes,
        args.semente,
        args.fracao_tea,
        args.fracao_feminina,
        args.discordancia_controle,
        args.discordancia_tea,
    )
    print(f'{args.participantes} participantes sintéticos em {caminhos[0]} e {caminhos[1]}')


if __name__ == '__main__':
    principal()
