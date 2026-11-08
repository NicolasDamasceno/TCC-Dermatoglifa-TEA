"""Aplica um modelo da Etapa 1 às imagens e grava o padroes.csv da Etapa 2 (ADR-0011).

Usa o mesmo carregamento (hash conferido, `safe_mode`) e o mesmo pré-processamento
do Serviço de IA (`dermatoglifia_ia`), e a mesma conversão de alças pela mão. Assim
a Etapa 2 é avaliada com os padrões que o sistema realmente produziria, erros da
CNN incluídos.

Entrada: o rotulos.csv das imagens (colunas arquivo, participante, mao, dedo; a
coluna classe, se houver, é ignorada) e a pasta do modelo (modelo.keras,
modelo.sha256, config.json). Saída: padroes.csv com participante, mao, dedo,
classe_imagem, padrao e confianca. Tudo fica na pasta de dados local.

    python treino/scripts/prever_padroes.py --rotulos <dados>/rotulos.csv \\
        --modelo modelo_producao/demo --saida <dados>/padroes.csv
"""

from __future__ import annotations

import argparse
import csv
import os
from pathlib import Path

os.environ.setdefault('TF_CPP_MIN_LOG_LEVEL', '2')

import numpy as np
from dermatoglifia_ia.dominio import ConvencaoAlca, Dedo, Mao, Posicao, converter
from dermatoglifia_ia.modelo import ModeloCarregado, OrigemModelo, carregar_pasta
from dermatoglifia_ia.preprocessamento import preparar_lote

LOTE = 64


def prever(
    modelo: ModeloCarregado, rotulos: Path, convencao: ConvencaoAlca = ConvencaoAlca.IMPRESSAO
) -> list[dict[str, str]]:
    with rotulos.open(encoding='utf-8', newline='') as arquivo:
        linhas = list(csv.DictReader(arquivo))
    faltando = {'arquivo', 'participante', 'mao', 'dedo'} - set(linhas[0] if linhas else {})
    if faltando:
        raise ValueError(f'{rotulos.name} precisa das colunas: {", ".join(sorted(faltando))}.')

    cfg = modelo.config
    resultado = []
    for inicio in range(0, len(linhas), LOTE):
        bloco = linhas[inicio : inicio + LOTE]
        conteudos = [(rotulos.parent / linha['arquivo']).read_bytes() for linha in bloco]
        lote = preparar_lote(conteudos, cfg.tamanho_imagem, cfg.ajuste_proporcao, cfg.interpolacao)
        for linha, probabilidades in zip(bloco, modelo.prever(lote), strict=True):
            posicao = Posicao(Mao(linha['mao'].strip()), Dedo(linha['dedo'].strip()))
            indice = int(np.argmax(probabilidades))
            classe = cfg.classes[indice]
            resultado.append(
                {
                    'participante': linha['participante'].strip(),
                    'mao': posicao.mao.value,
                    'dedo': posicao.dedo.value,
                    'classe_imagem': classe.value,
                    'padrao': converter(classe, posicao.mao, convencao).value,
                    'confianca': f'{probabilidades[indice]:.4f}',
                }
            )
    return resultado


def principal() -> None:
    p = argparse.ArgumentParser(description='Gera o padroes.csv da Etapa 2 com um modelo da Etapa 1.')
    p.add_argument('--rotulos', type=Path, required=True, help='rotulos.csv das imagens (fora do repositório)')
    p.add_argument('--modelo', type=Path, required=True, help='pasta com modelo.keras, modelo.sha256 e config.json')
    p.add_argument('--saida', type=Path, required=True, help='padroes.csv a gravar (fora do repositório)')
    p.add_argument('--convencao-alca', choices=[c.value for c in ConvencaoAlca], default='IMPRESSAO')
    args = p.parse_args()

    # A origem só rotula o objeto carregado; aqui não muda nada. O hash é conferido igual.
    modelo = carregar_pasta(args.modelo, OrigemModelo.DEMO)
    linhas = prever(modelo, args.rotulos, ConvencaoAlca(args.convencao_alca))
    args.saida.parent.mkdir(parents=True, exist_ok=True)
    with args.saida.open('w', newline='', encoding='utf-8') as arquivo:
        escritor = csv.DictWriter(arquivo, fieldnames=list(linhas[0]))
        escritor.writeheader()
        escritor.writerows(linhas)
    print(f'{len(linhas)} dedos previstos com {modelo.config.versao} em {args.saida}')
    print(f'Use --versao-etapa1 {modelo.config.versao} no etapa2.py.')


if __name__ == '__main__':
    principal()
