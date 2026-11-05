import csv

import numpy as np
import sinteticos
from PIL import Image


def test_cada_classe_gera_uma_digital_em_cinza():
    rng = np.random.default_rng(0)
    for classe in sinteticos.CLASSES:
        imagem = sinteticos.gerar_digital(classe, rng, lado=96)
        assert imagem.shape == (96, 96)
        assert imagem.dtype == np.uint8
        assert imagem.std() > 10  # tem cristas, não é um borrão


def test_alca_com_delta_a_esquerda_abre_para_a_direita():
    # Núcleo na origem e delta embaixo à esquerda: logo abaixo do núcleo as cristas
    # descem para a direita (orientação "\\"), as pernas da alça saem pela direita.
    theta = sinteticos.campo_zero_polo(320, [0j], [complex(-80, -80)], 0.0)
    abaixo_do_nucleo = np.mod(theta[160 + 50, 160], np.pi)  # linha 210 = 50 px abaixo do centro
    assert np.pi / 2 < abaixo_do_nucleo < np.pi  # entre 90° e 180°: inclinação descendente


def test_gerar_conjunto_tem_10_dedos_por_participante_e_500_ppi(tmp_path):
    rotulos = sinteticos.gerar_conjunto(tmp_path, participantes=3, semente=1, processos=1, lado=64)

    with rotulos.open(encoding='utf-8') as arquivo:
        linhas = list(csv.DictReader(arquivo))
    assert len(linhas) == 30
    assert {x['participante'] for x in linhas} == {'S00000', 'S00001', 'S00002'}
    assert all(x['classe'] in sinteticos.CLASSES for x in linhas)
    with Image.open(tmp_path / linhas[0]['arquivo']) as imagem:
        assert imagem.mode == 'L'
        assert round(imagem.info['dpi'][0]) == 500


def test_mesma_semente_mesmas_imagens(tmp_path):
    a = sinteticos.gerar_conjunto(tmp_path / 'a', participantes=1, semente=9, processos=1, lado=48)
    b = sinteticos.gerar_conjunto(tmp_path / 'b', participantes=1, semente=9, processos=1, lado=48)
    assert a.read_text() == b.read_text()
    primeira = 'imagens/S00000_DIREITA_POLEGAR.png'
    assert np.array_equal(np.array(Image.open(a.parent / primeira)), np.array(Image.open(b.parent / primeira)))
