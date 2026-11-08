"""prever_padroes com um modelo minúsculo e imagens de ruído: nada aqui é real."""

import csv
import json

import etapa1
import numpy as np
import prever_padroes
from dermatoglifia_ia.dominio import ORDEM_COLETA, ClasseImagem, ConvencaoAlca, Mao, converter
from dermatoglifia_ia.modelo import OrigemModelo, carregar_pasta, sha256_arquivo
from PIL import Image


def _modelo(pasta):
    pasta.mkdir()
    etapa1.construir_cnn((16, 16), blocos=1, filtros=4).save(pasta / 'modelo.keras')
    (pasta / 'modelo.sha256').write_text(sha256_arquivo(pasta / 'modelo.keras'))
    config = {'versao': 'minusculo_sintetico_v1', 'classes': list(etapa1.CLASSES), 'tamanho_imagem': [16, 16]}
    (pasta / 'config.json').write_text(json.dumps(config))
    return carregar_pasta(pasta, OrigemModelo.DEMO)


def _imagens(pasta, participantes=2):
    rng = np.random.default_rng(0)
    (pasta / 'imagens').mkdir(parents=True)
    with (pasta / 'rotulos.csv').open('w', newline='', encoding='utf-8') as f:
        escritor = csv.writer(f)
        escritor.writerow(['arquivo', 'participante', 'mao', 'dedo', 'classe'])
        for p in range(participantes):
            for posicao in ORDEM_COLETA:
                nome = f'imagens/S{p}_{posicao.nome_arquivo}.png'
                Image.fromarray(rng.integers(0, 255, (40, 32), dtype=np.uint8)).save(pasta / nome)
                escritor.writerow([nome, f'S{p}', posicao.mao, posicao.dedo, 'ARCO'])
    return pasta / 'rotulos.csv', participantes


def test_gera_um_padrao_por_dedo_com_a_conversao_do_servico(tmp_path):
    modelo = _modelo(tmp_path / 'modelo')
    rotulos, participantes = _imagens(tmp_path / 'dados')

    linhas = prever_padroes.prever(modelo, rotulos, ConvencaoAlca.IMPRESSAO)

    assert len(linhas) == participantes * 10
    for linha in linhas:
        esperado = converter(ClasseImagem(linha['classe_imagem']), Mao(linha['mao']), ConvencaoAlca.IMPRESSAO)
        assert linha['padrao'] == esperado.value
        assert 0 < float(linha['confianca']) <= 1
    assert {linha['participante'] for linha in linhas} == {'S0', 'S1'}
