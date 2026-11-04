import pytest

from dermatoglifia_ia.dominio import (
    ORDEM_COLETA,
    ConvencaoAlca,
    Dedo,
    Mao,
    Posicao,
    calcular_indicadores,
    converter,
)
from dermatoglifia_ia.dominio import (
    ClasseImagem as C,
)
from dermatoglifia_ia.dominio import (
    Padrao as P,
)


def test_ordem_da_ficha_e_nomes_do_contrato():
    assert len(ORDEM_COLETA) == 10
    assert ORDEM_COLETA[0].nome_arquivo == 'DIREITA_POLEGAR'
    assert ORDEM_COLETA[9].nome_arquivo == 'ESQUERDA_MINIMO'
    assert Posicao.do_nome('ESQUERDA_MEDIO') == Posicao(Mao.ESQUERDA, Dedo.MEDIO)
    with pytest.raises(ValueError):
        Posicao.do_nome('ESQUERDA-MEDIO')


# Mesma tabela do back-end (TabelaConversaoAlca.Impressao), que confere cada resposta.
@pytest.mark.parametrize(
    ('classe', 'mao', 'esperado'),
    [
        (C.ALCA_DIREITA, Mao.DIREITA, P.ALCA_ULNAR),
        (C.ALCA_DIREITA, Mao.ESQUERDA, P.ALCA_RADIAL),
        (C.ALCA_ESQUERDA, Mao.DIREITA, P.ALCA_RADIAL),
        (C.ALCA_ESQUERDA, Mao.ESQUERDA, P.ALCA_ULNAR),
        (C.ARCO, Mao.DIREITA, P.ARCO),
        (C.ARCO_TENDIDO, Mao.ESQUERDA, P.ARCO_TENDIDO),
        (C.VERTICILO, Mao.DIREITA, P.VERTICILO),
    ],
)
def test_conversao_na_convencao_de_impressao(classe, mao, esperado):
    assert converter(classe, mao, ConvencaoAlca.IMPRESSAO) == esperado


def test_convencao_espelhada_inverte_so_as_alcas():
    for mao in Mao:
        for classe in (C.ALCA_DIREITA, C.ALCA_ESQUERDA):
            normal = converter(classe, mao, ConvencaoAlca.IMPRESSAO)
            espelhada = converter(classe, mao, ConvencaoAlca.ESPELHADA)
            assert {normal, espelhada} == {P.ALCA_ULNAR, P.ALCA_RADIAL}
        assert converter(C.VERTICILO, mao, ConvencaoAlca.ESPELHADA) == P.VERTICILO


def test_indicadores_contam_padroes_e_discordancia_sem_e_com_polegar():
    padroes = {p: P.ALCA_ULNAR for p in ORDEM_COLETA}
    padroes[Posicao(Mao.DIREITA, Dedo.POLEGAR)] = P.VERTICILO  # discorda só no polegar
    padroes[Posicao(Mao.ESQUERDA, Dedo.INDICADOR)] = P.ARCO  # e no indicador
    padroes[Posicao(Mao.ESQUERDA, Dedo.MINIMO)] = P.ALCA_RADIAL  # e no mínimo

    i = calcular_indicadores(padroes)

    assert (i.qtd_alcas_ulnares, i.qtd_verticilos, i.qtd_arcos, i.qtd_alcas_radiais, i.qtd_arcos_tendidos) == (
        7,
        1,
        1,
        1,
        0,
    )
    assert i.discordancia_bilateral == 2  # 0 a 4, polegar excluído (de Bruin et al. 2014)
    assert i.discordancia_bilateral_com_polegar == 3


def test_indicadores_exigem_os_10_dedos():
    with pytest.raises(ValueError):
        calcular_indicadores({ORDEM_COLETA[0]: P.ARCO})
