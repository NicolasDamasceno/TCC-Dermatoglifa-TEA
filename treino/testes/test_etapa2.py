"""Etapa 2 com dados SINTÉTICOS (CLAUDE.md, Regra 4): nenhum grupo aqui é real."""

import csv
import json
import math

import etapa2
import numpy as np
import pytest
import sinteticos_etapa2
from dermatoglifia_ia.dominio import ORDEM_COLETA, Dedo, Mao, Padrao, Posicao, calcular_indicadores


def _gerar(pasta, participantes=200, semente=1, controle=0.45, tea=0.56):
    return sinteticos_etapa2.gerar_conjunto(
        pasta, participantes, semente, discordancia_controle=controle, discordancia_tea=tea
    )


def _rodar(tmp_path, dados, *extras, versao='teste_v1'):
    return etapa2.principal(
        [
            '--dados', str(dados),
            '--origem', 'SINTETICO',
            '--origem-padroes', 'ANOTACAO',
            '--destino-modelos', str(tmp_path / 'modelos'),
            '--versao', versao,
            '--reamostragens', '200',
            *extras,
        ]
    )  # fmt: skip


def test_gerador_reproduz_a_discordancia_media_pedida(tmp_path):
    _, arquivo_padroes = _gerar(tmp_path, participantes=2000, controle=0.2, tea=0.7)
    padroes = etapa2.ler_padroes(arquivo_padroes)
    with (tmp_path / 'participantes.csv').open(encoding='utf-8') as f:
        grupos = {linha['participante']: linha['grupo'] for linha in csv.DictReader(f)}

    medias = {}
    for grupo in ('CONTROLE', 'TEA'):
        valores = [calcular_indicadores(padroes[c]).discordancia_bilateral for c, g in grupos.items() if g == grupo]
        medias[grupo] = np.mean(valores)

    assert medias['CONTROLE'] == pytest.approx(4 * 0.2, abs=0.1)  # Binomial(4, p)
    assert medias['TEA'] == pytest.approx(4 * 0.7, abs=0.1)


def test_padrao_do_gerador_e_o_das_medias_de_de_bruin():
    assert pytest.approx(1.80) == 4 * sinteticos_etapa2.DISCORDANCIA_CONTROLE
    assert pytest.approx(2.24) == 4 * sinteticos_etapa2.DISCORDANCIA_TEA


def test_indicadores_vem_do_mesmo_codigo_do_servico(tmp_path):
    arquivo_participantes, arquivo_padroes = _gerar(tmp_path, participantes=5)
    padroes = etapa2.ler_padroes(arquivo_padroes)
    participantes, excluidos = etapa2.montar_participantes(
        etapa2.ler_csv(arquivo_participantes, ('participante', 'sexo', 'idade_meses', 'grupo')), padroes
    )

    assert excluidos == {'sem_os_10_padroes': 0}
    for p in participantes:
        esperado = calcular_indicadores(padroes[p.codigo])
        assert p.variaveis['discordancia_bilateral'] == esperado.discordancia_bilateral
        assert p.variaveis['qtd_verticilos'] == esperado.qtd_verticilos
        assert p.variaveis['sexo_feminino'] in (0.0, 1.0)


def test_quem_nao_tem_os_10_dedos_sai_e_e_contado():
    linhas = [
        {'participante': 'A', 'sexo': 'MASCULINO', 'idade_meses': '60', 'grupo': 'TEA'},
        {'participante': 'B', 'sexo': 'FEMININO', 'idade_meses': '72', 'grupo': 'CONTROLE'},
    ]
    completos = dict.fromkeys(ORDEM_COLETA, Padrao.ALCA_ULNAR)
    incompletos = {Posicao(Mao.DIREITA, Dedo.POLEGAR): Padrao.ARCO}

    participantes, excluidos = etapa2.montar_participantes(linhas, {'A': completos, 'B': incompletos})

    assert [p.codigo for p in participantes] == ['A']
    assert excluidos == {'sem_os_10_padroes': 1}


@pytest.mark.parametrize(
    ('linha', 'mensagem'),
    [
        ({'sexo': 'OUTRO'}, 'sexo'),
        ({'grupo': 'SUSPEITA'}, 'grupo'),
        ({'idade_meses': '-3'}, 'idade_meses'),
    ],
)
def test_recusa_valores_fora_do_dominio(linha, mensagem):
    base = {'participante': 'A', 'sexo': 'MASCULINO', 'idade_meses': '60', 'grupo': 'TEA'}
    with pytest.raises(ValueError, match=mensagem):
        etapa2.montar_participantes([{**base, **linha}], {})


def test_recusa_padroes_de_quem_nao_esta_na_lista_de_participantes():
    with pytest.raises(ValueError, match=r'não estão em participantes\.csv'):
        etapa2.montar_participantes([], {'X': dict.fromkeys(ORDEM_COLETA, Padrao.ARCO)})


def _participantes(n, semente=0):
    rng = np.random.default_rng(semente)
    return [
        etapa2.Participante(f'P{i}', int(i % 2), {'discordancia_bilateral': float(rng.integers(0, 5))})
        for i in range(n)
    ]


def test_divisao_por_participante_sem_sobreposicao_e_com_os_dois_grupos():
    participantes = _participantes(40)
    treino, teste = etapa2.dividir(participantes, 0.25, semente=3)

    assert len(teste) == 10 and len(treino) == 30
    assert not {p.codigo for p in treino} & {p.codigo for p in teste}
    assert {p.grupo for p in teste} == {0, 1}


def test_quem_treinou_a_cnn_nunca_vai_para_o_teste_da_etapa2():
    participantes = _participantes(40)
    da_cnn = {f'P{i}' for i in range(20)}  # treino e validação da Etapa 1

    for semente in range(5):
        _, teste = etapa2.dividir(participantes, 0.25, semente, fora_do_teste=da_cnn)
        assert not {p.codigo for p in teste} & da_cnn


def test_divisao_avisa_quando_nao_ha_participantes_fora_da_cnn():
    participantes = _participantes(40)
    with pytest.raises(ValueError, match='ficaram fora do treino'):
        etapa2.dividir(participantes, 0.25, 0, fora_do_teste={f'P{i}' for i in range(35)})


def test_intervalo_de_wilson_confere_com_o_valor_de_referencia():
    # 8 de 10: Wilson 95% ≈ 0,490 a 0,943 (Newcombe 1998, método 3)
    inferior, superior = etapa2.wilson(8, 10)
    assert inferior == pytest.approx(0.4902, abs=1e-3)
    assert superior == pytest.approx(0.9433, abs=1e-3)
    assert etapa2.wilson(0, 10)[0] == 0.0
    assert etapa2.wilson(0, 0) is None


def test_vpp_e_vpn_reproduzem_o_calculo_do_doc_07():
    # sensibilidade 0,70, especificidade 0,60, prevalência 1/31 → VPP ≈ 5,5%, VPN ≈ 98%
    vpp, vpn = etapa2.preditivos(0.70, 0.60, 1 / 31)
    assert vpp == pytest.approx(0.055, abs=0.001)
    assert vpn == pytest.approx(0.984, abs=0.002)


def test_calibracao_reconhece_previsoes_calibradas_e_extremas():
    rng = np.random.default_rng(0)
    logito = rng.normal(0, 1.5, 20000)
    prob = 1 / (1 + np.exp(-logito))
    y = (rng.random(20000) < prob).astype(int)

    calibrada = etapa2.calibracao(y, prob)
    extrema = etapa2.calibracao(y, 1 / (1 + np.exp(-2 * logito)))  # previsões extremas demais

    assert calibrada['intercepto'] == pytest.approx(0, abs=0.06)
    assert calibrada['inclinacao'] == pytest.approx(1, abs=0.06)
    assert extrema['inclinacao'] == pytest.approx(0.5, abs=0.04)  # inclinação < 1 (Van Calster 2019)


def test_coeficientes_na_escala_original_reproduzem_o_modelo():
    rng = np.random.default_rng(1)
    x = np.column_stack([rng.integers(0, 5, 200), rng.integers(0, 2, 200), rng.integers(24, 145, 200)]).astype(float)
    y = (rng.random(200) < 1 / (1 + np.exp(-(x[:, 0] - 2)))).astype(int)
    variaveis = ('discordancia_bilateral', 'sexo_feminino', 'idade_meses')
    modelo = etapa2.ajustar(x, y, variaveis, c=1.0)

    coefs = etapa2.coeficientes(modelo)
    logito = coefs['intercepto'] + x @ np.array([coefs['coeficientes'][v] for v in variaveis])

    np.testing.assert_allclose(1 / (1 + np.exp(-logito)), modelo.prever(x), atol=1e-9)
    assert coefs['razao_de_chances_por_unidade']['discordancia_bilateral'] == pytest.approx(
        math.exp(coefs['coeficientes']['discordancia_bilateral'])
    )


def test_sem_efeito_o_modelo_nao_acha_sinal(tmp_path):
    _gerar(tmp_path / 'dados', participantes=400, controle=0.5, tea=0.5)
    pasta = _rodar(tmp_path, tmp_path / 'dados')
    teste = json.loads((pasta / 'metricas.json').read_text(encoding='utf-8'))['teste']

    assert teste['auc_roc']['ic95'][0] < 0.5 < teste['auc_roc']['ic95'][1]
    assert teste['ganho_auc_roc']['ic95'][0] < 0 < teste['ganho_auc_roc']['ic95'][1]


def test_com_efeito_forte_o_modelo_acha_e_grava_a_versao(tmp_path):
    _gerar(tmp_path / 'dados', participantes=200, controle=0.1, tea=0.9)
    pasta = _rodar(tmp_path, tmp_path / 'dados')

    metricas = json.loads((pasta / 'metricas.json').read_text(encoding='utf-8'))
    coefs = json.loads((pasta / 'coeficientes.json').read_text(encoding='utf-8'))
    config = json.loads((pasta / 'config.json').read_text(encoding='utf-8'))

    assert pasta == tmp_path / 'modelos' / 'sinteticos' / 'teste_v1'
    assert metricas['teste']['auc_roc']['valor'] > 0.9
    assert metricas['teste']['ganho_auc_roc']['ic95'][0] > 0
    assert coefs['validado'] is False  # RN10
    assert coefs['razao_de_chances_por_unidade']['discordancia_bilateral'] > 1
    assert config['origem_padroes'] == 'ANOTACAO' and config['etapa'] == 2
    assert (pasta / 'graficos' / 'roc.svg').exists() and (pasta / 'graficos' / 'calibracao.svg').exists()
    assert 'não dizem nada sobre TEA' in (pasta / 'NOTAS.md').read_text(encoding='utf-8')


def test_o_que_vai_para_o_git_nao_tem_codigo_de_participante(tmp_path):
    _gerar(tmp_path / 'dados', participantes=120, controle=0.2, tea=0.8)
    pasta = _rodar(tmp_path, tmp_path / 'dados')

    for arquivo in pasta.rglob('*'):
        if arquivo.is_file():
            assert 'E000' not in arquivo.read_text(encoding='utf-8'), arquivo.name
    divisao_local = tmp_path / 'dados' / 'saidas' / 'teste_v1' / 'divisao.json'
    assert 'E000' in divisao_local.read_text(encoding='utf-8')  # só na pasta local


def test_variavel_constante_no_treino_sai_do_modelo(tmp_path):
    sinteticos_etapa2.gerar_conjunto(tmp_path / 'dados', 120, 2, fracao_feminina=0.0)
    pasta = _rodar(tmp_path, tmp_path / 'dados')
    config = json.loads((pasta / 'config.json').read_text(encoding='utf-8'))

    assert config['variaveis_retiradas_por_serem_constantes'] == ['sexo_feminino']
    assert 'sexo_feminino' not in json.loads((pasta / 'coeficientes.json').read_text(encoding='utf-8'))['variaveis']


def test_recusa_dados_dentro_do_repositorio(tmp_path):
    with pytest.raises(SystemExit, match='fora do repositório'):
        _rodar(tmp_path, etapa2.RAIZ_REPOSITORIO / 'treino')


def test_recusa_saida_local_dentro_do_repositorio(tmp_path):
    _gerar(tmp_path / 'dados', participantes=40)
    with pytest.raises(SystemExit, match='fora do repositório'):
        _rodar(tmp_path, tmp_path / 'dados', '--saida-local', str(etapa2.RAIZ_REPOSITORIO / 'treino' / 'saida'))
    assert not (etapa2.RAIZ_REPOSITORIO / 'treino' / 'saida').exists()


def test_padroes_da_etapa1_exigem_a_divisao_da_cnn(tmp_path):
    with pytest.raises(SystemExit):
        etapa2.argumentos(['--origem', 'SINTETICO', '--origem-padroes', 'ETAPA1', '--versao-etapa1', 'v1'])


def test_teste_da_etapa2_exclui_quem_treinou_a_cnn(tmp_path):
    _gerar(tmp_path / 'dados', participantes=120, controle=0.2, tea=0.8)
    da_cnn = [f'E{i:05d}' for i in range(60)]
    divisao_cnn = tmp_path / 'divisao.json'
    divisao_cnn.write_text(json.dumps({'treino': da_cnn[:50], 'validacao': da_cnn[50:], 'teste': []}))

    etapa2.principal(
        [
            '--dados', str(tmp_path / 'dados'),
            '--origem', 'SINTETICO',
            '--origem-padroes', 'ETAPA1',
            '--versao-etapa1', 'cnn_sintetica_v1',
            '--divisao-etapa1', str(divisao_cnn),
            '--destino-modelos', str(tmp_path / 'modelos'),
            '--versao', 'teste_v1',
            '--reamostragens', '50',
        ]
    )  # fmt: skip

    teste = json.loads((tmp_path / 'dados' / 'saidas' / 'teste_v1' / 'divisao.json').read_text())['teste']
    assert teste and not set(teste) & set(da_cnn)
