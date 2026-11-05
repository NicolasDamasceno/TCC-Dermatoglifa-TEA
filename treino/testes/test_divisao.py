from pathlib import Path

import divisao
import pytest


def _exemplos(participantes=40):
    classes = ['ARCO', 'ARCO_TENDIDO', 'ALCA_ESQUERDA', 'ALCA_DIREITA', 'VERTICILO']
    return [
        divisao.Exemplo(Path(f'/x/P{p}_{d}.png'), f'P{p:03d}', classes[(p + d) % 5])
        for p in range(participantes)
        for d in range(10)
    ]


def test_nenhum_participante_em_dois_conjuntos_e_ninguem_fica_de_fora():
    exemplos = _exemplos()
    partes = divisao.dividir(exemplos)

    participantes = {nome: {e.participante for e in lista} for nome, lista in partes.items()}
    assert not participantes['treino'] & participantes['teste']
    assert not participantes['treino'] & participantes['validacao']
    assert not participantes['validacao'] & participantes['teste']
    assert sum(len(lista) for lista in partes.values()) == len(exemplos)
    # cada participante leva os 10 dedos juntos
    for lista in partes.values():
        for p in {e.participante for e in lista}:
            assert sum(e.participante == p for e in lista) == 10


def test_proporcoes_aproximadas():
    partes = divisao.dividir(_exemplos(100))
    total = 1000
    assert 0.6 < len(partes['treino']) / total < 0.8
    assert 0.08 < len(partes['teste']) / total < 0.22


def test_resumo_nao_tem_codigo_de_participante():
    resumo = divisao.resumo(divisao.dividir(_exemplos()))
    assert 'P001' not in str(resumo)
    assert set(resumo['teste']) == {'imagens', 'participantes', 'por_classe'}


def test_estrutura_virtual_uma_pasta_por_classe(tmp_path):
    origem = tmp_path / 'imagens'
    origem.mkdir()
    exemplos = []
    for p in range(14):
        for d in range(10):
            arquivo = origem / f'P{p}_{d}.png'
            arquivo.write_bytes(b'png sintetico')
            exemplos.append(divisao.Exemplo(arquivo, f'P{p}', ['ARCO', 'VERTICILO'][d % 2]))

    pastas = divisao.montar_estrutura_virtual(divisao.dividir(exemplos), tmp_path / 'virtual')

    for pasta in pastas.values():
        assert {p.name for p in pasta.iterdir()} <= {'ARCO', 'VERTICILO'}
    arquivos = list((tmp_path / 'virtual').rglob('*.png'))
    assert len(arquivos) == 140
    assert arquivos[0].read_bytes() == b'png sintetico'


def test_rotulos_sem_colunas_obrigatorias(tmp_path):
    csv = tmp_path / 'rotulos.csv'
    csv.write_text('arquivo,classe\nx.png,ARCO\n', encoding='utf-8')
    with pytest.raises(ValueError, match='participante'):
        divisao.ler_rotulos(csv)
