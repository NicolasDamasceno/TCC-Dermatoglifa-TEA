"""Contrato do ADR-0001, do lado do serviço, com modelo e imagens sintéticos."""

import pytest
from conftest import CLASSES, brilho_da_classe, png_listrado, png_uniforme
from fastapi.testclient import TestClient

from dermatoglifia_ia.dominio import ORDEM_COLETA, ConvencaoAlca
from servico.app import criar_app
from servico.configuracao import Configuracao

CHAVES_DIGITAL = {'mao', 'dedo', 'classe_imagem', 'padrao', 'confianca', 'probabilidades', 'alerta_qualidade'}
CHAVES_INDICADORES = {
    'qtd_verticilos',
    'qtd_alcas_ulnares',
    'qtd_alcas_radiais',
    'qtd_arcos',
    'qtd_arcos_tendidos',
    'discordancia_bilateral',
    'discordancia_bilateral_com_polegar',
}


def _config(tmp_path, pasta_demo, **extra):
    base = {
        'pasta_producao': tmp_path / 'sem-producao',
        'pasta_demo': pasta_demo,
        'convencao_alca': ConvencaoAlca.IMPRESSAO,
        'limiar_confianca': 0.5,
        'limiar_contraste': 10.0,
    }
    base.update(extra)
    return Configuracao(**base)


@pytest.fixture
def cliente(tmp_path, pasta_modelo):
    with TestClient(criar_app(_config(tmp_path, pasta_modelo))) as c:
        yield c


def _requisicao(classes_por_dedo=None, sexo='FEMININO', idade='55', sem=None, extra=None, imagem=None):
    """Monta o multipart como a API .NET (ClienteInferenciaHttp)."""
    classes_por_dedo = classes_por_dedo or {}
    arquivos = []
    for posicao in ORDEM_COLETA:
        if posicao.nome_arquivo == sem:
            continue
        classe = classes_por_dedo.get(posicao.nome_arquivo, 'VERTICILO')
        conteudo = imagem if imagem is not None else png_listrado(brilho_da_classe(classe))
        arquivos.append((posicao.nome_arquivo, (f'{posicao.nome_arquivo}.png', conteudo, 'image/png')))
    dados = {'sexo': sexo, 'idade_meses': idade}
    dados.update(extra or {})
    return {'files': arquivos, 'data': dados}


def test_saude_informa_o_modelo_em_uso(cliente):
    r = cliente.get('/saude')
    assert r.status_code == 200
    corpo = r.json()
    assert corpo['status'] == 'OK'
    assert (corpo['versao_modelo'], corpo['origem'], corpo['origem_dados']) == (
        'teste_sintetico_v1',
        'DEMO',
        'SINTETICO',
    )
    assert len(corpo['sha256']) == 64


def test_inferencia_segue_o_contrato_e_converte_as_alcas(cliente):
    classes = {
        'DIREITA_POLEGAR': 'VERTICILO',
        'DIREITA_INDICADOR': 'ALCA_DIREITA',  # ulnar na mão direita
        'ESQUERDA_INDICADOR': 'ALCA_DIREITA',  # radial na mão esquerda
        'DIREITA_MEDIO': 'ARCO',
        'ESQUERDA_MEDIO': 'ARCO',
        'ESQUERDA_ANELAR': 'ARCO_TENDIDO',
        'ESQUERDA_MINIMO': 'ALCA_ESQUERDA',  # ulnar na mão esquerda
    }
    r = cliente.post('/inferencia', **_requisicao(classes))

    assert r.status_code == 200, r.text
    corpo = r.json()
    assert set(corpo) == {'versao_modelo', 'digitais', 'indicadores', 'escore_risco'}
    assert corpo['versao_modelo'] == 'teste_sintetico_v1'
    assert corpo['escore_risco'] is None  # Etapa 2 não validada (RN10)

    por_nome = {f'{d["mao"]}_{d["dedo"]}': d for d in corpo['digitais']}
    assert list(por_nome) == [p.nome_arquivo for p in ORDEM_COLETA]
    for nome, digital in por_nome.items():
        assert set(digital) == CHAVES_DIGITAL
        assert digital['classe_imagem'] == classes.get(nome, 'VERTICILO')
        assert set(digital['probabilidades']) == set(CLASSES)
        assert abs(sum(digital['probabilidades'].values()) - 1) <= 0.01  # tolerância da API
        assert digital['confianca'] == max(digital['probabilidades'].values())
        assert digital['alerta_qualidade'] is False

    assert por_nome['DIREITA_INDICADOR']['padrao'] == 'ALCA_ULNAR'
    assert por_nome['ESQUERDA_INDICADOR']['padrao'] == 'ALCA_RADIAL'
    assert por_nome['ESQUERDA_MINIMO']['padrao'] == 'ALCA_ULNAR'

    assert set(corpo['indicadores']) == CHAVES_INDICADORES
    assert corpo['indicadores'] == {
        'qtd_arcos': 2,
        'qtd_arcos_tendidos': 1,
        'qtd_alcas_ulnares': 2,
        'qtd_alcas_radiais': 1,
        'qtd_verticilos': 4,
        # indicador (ulnar × radial), anelar (verticilo × arco tendido), mínimo (verticilo × ulnar)
        'discordancia_bilateral': 3,
        'discordancia_bilateral_com_polegar': 3,
    }


def test_convencao_espelhada_vem_da_configuracao(tmp_path, pasta_modelo):
    config = _config(tmp_path, pasta_modelo, convencao_alca=ConvencaoAlca.ESPELHADA)
    with TestClient(criar_app(config)) as c:
        r = c.post('/inferencia', **_requisicao({'DIREITA_INDICADOR': 'ALCA_DIREITA'}))
    digital = next(d for d in r.json()['digitais'] if d['mao'] == 'DIREITA' and d['dedo'] == 'INDICADOR')
    assert digital['padrao'] == 'ALCA_RADIAL'


def test_imagem_quase_uniforme_recebe_alerta_de_qualidade(cliente):
    r = cliente.post('/inferencia', **_requisicao(imagem=png_uniforme()))
    assert r.status_code == 200
    assert all(d['alerta_qualidade'] for d in r.json()['digitais'])


@pytest.mark.parametrize(
    ('ajuste', 'trecho'),
    [
        ({'sem': 'ESQUERDA_MINIMO'}, 'Falta a imagem ESQUERDA_MINIMO'),
        ({'extra': {'nome': 'Fulano'}}, 'Campos não previstos no contrato: nome'),
        ({'sexo': 'OUTRO'}, 'sexo'),
        ({'idade': '-3'}, 'idade_meses'),
        ({'idade': 'cinco'}, 'idade_meses'),
        ({'imagem': b'nao sou png'}, 'DIREITA_POLEGAR: O arquivo não é PNG.'),
    ],
)
def test_recusa_o_que_foge_do_contrato(cliente, ajuste, trecho):
    r = cliente.post('/inferencia', **_requisicao(**ajuste))
    assert r.status_code == 422
    assert trecho in r.json()['detail']


def test_recusa_tipo_de_conteudo_errado(cliente):
    requisicao = _requisicao()
    nome, (arquivo, conteudo, _) = requisicao['files'][0]
    requisicao['files'][0] = (nome, (arquivo, conteudo, 'image/jpeg'))
    r = cliente.post('/inferencia', **requisicao)
    assert r.status_code == 422
    assert 'image/png' in r.json()['detail']


def test_recusa_corpo_que_nao_e_multipart(cliente):
    r = cliente.post('/inferencia', json={'sexo': 'FEMININO'})
    assert r.status_code == 422


def test_sem_modelo_responde_503_e_explica_na_saude(tmp_path):
    config = _config(tmp_path, tmp_path / 'sem-demo')
    with TestClient(criar_app(config)) as c:
        saude = c.get('/saude')
        inferencia = c.post('/inferencia', **_requisicao())
    assert saude.status_code == 503
    assert 'Nenhum modelo' in saude.json()['motivo']
    assert inferencia.status_code == 503


def test_sem_paginas_de_documentacao_expostas(cliente):
    assert cliente.get('/docs').status_code == 404
    assert cliente.get('/openapi.json').status_code == 404


def test_limita_quantidade_de_arquivos(cliente):
    requisicao = _requisicao()
    requisicao['files'].append(('OUTRO', ('x.png', png_uniforme(), 'image/png')))
    r = cliente.post('/inferencia', **requisicao)
    assert r.status_code == 422
    assert 'Formulário inválido' in r.json()['detail']
