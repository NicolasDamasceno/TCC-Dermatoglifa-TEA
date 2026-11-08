"""Etapa 2 — escore complementar por regressão logística (ADR-0002; ADR-0011).

Recebe, por participante, o padrão de cada um dos 10 dedos (da anotação manual
ou da Etapa 1, via `prever_padroes.py`), sexo, idade e grupo (TEA ou controle),
calcula os indicadores com o MESMO código do Serviço de IA
(`dermatoglifia_ia.dominio.calcular_indicadores`) e treina uma regressão
logística interpretável. O que este script faz, e por quê:

- variáveis PRÉ-ESPECIFICADAS (`--variaveis`): o conjunto `minimo` usa só a
  discordância bilateral, o único achado replicado (de Bruin et al. 2014;
  Kyselicová et al. 2023; doc 07), mais sexo e idade. Escolher variáveis olhando
  o resultado infla o desempenho;
- divisão por participante: cada linha é uma pessoa; o teste é separado antes de
  tudo e só é usado uma vez. Com `--divisao-etapa1`, o teste só recebe quem não
  esteve no treino nem na validação da CNN (ADR-0002);
- regularização L2 escolhida por validação cruzada estratificada SÓ no treino;
  o ponto de corte (Youden) também sai do treino, das previsões fora da dobra;
- métricas do protocolo (ADR-0002; TRIPOD+AI): sensibilidade e especificidade
  com IC 95% de Wilson (Newcombe 1998), ROC-AUC e PR-AUC com IC por bootstrap,
  VPP e VPN nas prevalências declaradas, calibração por intercepto e inclinação
  (Van Calster et al. 2019) e Brier;
- comparação com um modelo só de covariáveis (sexo e idade): num estudo
  caso-controle, idade e sexo podem separar os grupos por causa da amostragem,
  não da dermatoglifia. O que interessa é o ganho dos indicadores sobre ele;
- tamanho da amostra: eventos por parâmetro candidato e precisão da proporção
  (Riley et al. 2020) são impressos e gravados, sem substituir o cálculo
  completo (pacote pmsampsize).

Uso típico (dados sintéticos, só para conferir o código):

    python treino/scripts/sinteticos_etapa2.py --participantes 300
    python treino/scripts/etapa2.py --dados <TCC_DADOS_DIR>/sinteticos_etapa2 --origem SINTETICO \\
        --origem-padroes ANOTACAO
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import os
import platform
import re
import warnings
from dataclasses import asdict, dataclass
from datetime import date
from pathlib import Path

import matplotlib

matplotlib.use('Agg')  # só arquivos; nenhuma janela
import matplotlib.pyplot as plt
import numpy as np
import sklearn
from dermatoglifia_ia.dominio import Dedo, Mao, Padrao, Posicao, calcular_indicadores
from scipy.special import expit
from sklearn.exceptions import ConvergenceWarning
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import average_precision_score, brier_score_loss, log_loss, roc_auc_score, roc_curve
from sklearn.model_selection import StratifiedKFold, cross_val_predict
from sklearn.pipeline import Pipeline, make_pipeline
from sklearn.preprocessing import StandardScaler

RAIZ_REPOSITORIO = Path(__file__).resolve().parents[2]
VERSAO_VALIDA = re.compile(r'^[A-Za-z0-9_.-]{1,80}$')

GRUPOS = ('CONTROLE', 'TEA')  # codificados como 0 e 1
SEXOS = ('MASCULINO', 'FEMININO')
IDADE_MAXIMA_MESES = 120 * 12

# A alça ulnar fica de fora como referência: as cinco contagens somam 10.
CONTAGENS = ('qtd_arcos', 'qtd_arcos_tendidos', 'qtd_alcas_radiais', 'qtd_verticilos')
COVARIAVEIS = ('sexo_feminino', 'idade_meses')
CONJUNTOS_VARIAVEIS = {
    'minimo': ('discordancia_bilateral', *COVARIAVEIS),
    'contagens': ('discordancia_bilateral', *CONTAGENS, *COVARIAVEIS),
}
# 1/31: vigilância ADDM 2022 do CDC (doc 07). 0,30: cenário hipotético de população
# encaminhada usado no doc 07. Não há estimativa brasileira (doc 07, NÃO VERIFICADO).
PREVALENCIAS_PADRAO = (1 / 31, 0.30)
GRADE_C = (0.01, 0.03, 0.1, 0.3, 1.0, 3.0, 10.0)
Z_95 = 1.959963984540054


# 1 - Leitura dos dados ----------------------------------------------------------------


@dataclass(frozen=True)
class Participante:
    codigo: str
    grupo: int  # 1 = TEA
    variaveis: dict[str, float]


def ler_csv(caminho: Path, colunas: tuple[str, ...]) -> list[dict[str, str]]:
    with caminho.open(encoding='utf-8', newline='') as arquivo:
        linhas = list(csv.DictReader(arquivo))
    faltando = set(colunas) - set(linhas[0] if linhas else {})
    if faltando:
        raise ValueError(f'{caminho.name} precisa das colunas: {", ".join(sorted(faltando))}.')
    return [{k: (v or '').strip() for k, v in linha.items()} for linha in linhas]


def ler_padroes(caminho: Path) -> dict[str, dict[Posicao, Padrao]]:
    """padroes.csv: participante, mao, dedo, padrao (já convertido para ulnar/radial)."""
    padroes: dict[str, dict[Posicao, Padrao]] = {}
    for numero, linha in enumerate(ler_csv(caminho, ('participante', 'mao', 'dedo', 'padrao')), start=2):
        try:
            posicao = Posicao(Mao(linha['mao']), Dedo(linha['dedo']))
            padrao = Padrao(linha['padrao'])
        except ValueError as erro:
            raise ValueError(f'{caminho.name}, linha {numero}: {erro}') from erro
        dedos = padroes.setdefault(linha['participante'], {})
        if posicao in dedos:
            raise ValueError(f'{caminho.name}, linha {numero}: {posicao.nome_arquivo} repetido para o participante.')
        dedos[posicao] = padrao
    return padroes


def montar_participantes(
    linhas: list[dict[str, str]], padroes: dict[str, dict[Posicao, Padrao]]
) -> tuple[list[Participante], dict[str, int]]:
    """Junta participantes e padrões; quem não tem os 10 dedos sai e é contado (RN09)."""
    participantes, excluidos = [], {'sem_os_10_padroes': 0}
    codigos = set()
    for numero, linha in enumerate(linhas, start=2):
        codigo = linha['participante']
        if codigo in codigos:
            raise ValueError(f'participantes.csv, linha {numero}: participante repetido.')
        codigos.add(codigo)
        if linha['sexo'] not in SEXOS:
            raise ValueError(f'participantes.csv, linha {numero}: sexo deve ser {" ou ".join(SEXOS)}.')
        if linha['grupo'] not in GRUPOS:
            raise ValueError(f'participantes.csv, linha {numero}: grupo deve ser {" ou ".join(GRUPOS)}.')
        if not linha['idade_meses'].isdigit() or int(linha['idade_meses']) > IDADE_MAXIMA_MESES:
            raise ValueError(f'participantes.csv, linha {numero}: idade_meses deve ser inteiro de 0 a 1440.')

        dedos = padroes.get(codigo, {})
        if len(dedos) != 10:
            excluidos['sem_os_10_padroes'] += 1
            continue
        variaveis: dict[str, float] = {k: float(v) for k, v in asdict(calcular_indicadores(dedos)).items()}
        variaveis['sexo_feminino'] = float(linha['sexo'] == 'FEMININO')
        variaveis['idade_meses'] = float(linha['idade_meses'])
        participantes.append(Participante(codigo, GRUPOS.index(linha['grupo']), variaveis))

    sobrando = set(padroes) - codigos
    if sobrando:
        raise ValueError(f'padroes.csv tem {len(sobrando)} participante(s) que não estão em participantes.csv.')
    return participantes, excluidos


def matriz(participantes: list[Participante], variaveis: tuple[str, ...]) -> tuple[np.ndarray, np.ndarray]:
    x = np.array([[p.variaveis[v] for v in variaveis] for p in participantes], dtype=np.float64)
    y = np.array([p.grupo for p in participantes], dtype=np.int64)
    return x.reshape(len(participantes), len(variaveis)), y


# 2 - Divisão por participante ---------------------------------------------------------


def dividir(
    participantes: list[Participante],
    fracao_teste: float,
    semente: int,
    fora_do_teste: set[str] | None = None,
) -> tuple[list[Participante], list[Participante]]:
    """Separa o teste antes de tudo, com a proporção de TEA da amostra inteira.

    `fora_do_teste`: participantes que estiveram no treino ou na validação da
    Etapa 1; nunca vão para o teste da Etapa 2 (ADR-0002).
    """
    fora = fora_do_teste or set()
    tamanho = round(fracao_teste * len(participantes))
    tea = round(tamanho * sum(p.grupo for p in participantes) / max(1, len(participantes)))
    pedidos = {0: tamanho - tea, 1: tea}
    if min(pedidos.values()) < 2:
        raise ValueError('Amostra pequena demais para separar um teste com pelo menos 2 participantes de cada grupo.')

    rng = np.random.default_rng(semente)
    teste: list[Participante] = []
    for grupo, quantos in pedidos.items():
        elegiveis = [p for p in participantes if p.grupo == grupo and p.codigo not in fora]
        if quantos > len(elegiveis):
            raise ValueError(
                f'O teste precisa de {quantos} participantes do grupo {GRUPOS[grupo]}, mas só {len(elegiveis)} '
                'ficaram fora do treino e da validação da Etapa 1.'
            )
        teste += [elegiveis[i] for i in sorted(rng.choice(len(elegiveis), quantos, replace=False))]
    codigos_teste = {p.codigo for p in teste}
    treino = [p for p in participantes if p.codigo not in codigos_teste]
    return treino, teste


# 3 - Modelo ---------------------------------------------------------------------------


def construir_modelo(c: float) -> Pipeline:
    """Padronização + regressão logística com penalidade L2 (encolhimento; Riley et al. 2020)."""
    return make_pipeline(StandardScaler(), LogisticRegression(C=c, max_iter=5000))


def dobras_para(y: np.ndarray) -> int:
    dobras = min(5, int(np.bincount(y, minlength=2).min()))
    if dobras < 2:
        raise ValueError('O treino precisa de pelo menos 2 participantes de cada grupo para a validação cruzada.')
    return dobras


def escolher_c(x: np.ndarray, y: np.ndarray, semente: int) -> tuple[float, np.ndarray, dict[str, float]]:
    """C pela menor log-loss fora da dobra; empate fica com o C menor (mais encolhimento)."""
    divisor = StratifiedKFold(n_splits=dobras_para(y), shuffle=True, random_state=semente)
    perdas: dict[str, float] = {}
    previsoes: dict[float, np.ndarray] = {}
    for c in GRADE_C:
        if x.shape[1] == 0:
            previsoes[c] = np.full(len(y), y.mean())
        else:
            previsoes[c] = cross_val_predict(construir_modelo(c), x, y, cv=divisor, method='predict_proba')[:, 1]
        perdas[str(c)] = log_loss(y, previsoes[c], labels=[0, 1])
    # min devolve o primeiro mínimo: no empate, o C menor (a grade está em ordem crescente)
    melhor = min(GRADE_C, key=lambda c: round(perdas[str(c)], 12))
    return melhor, previsoes[melhor], perdas


def ponto_de_corte_youden(y: np.ndarray, probabilidades: np.ndarray) -> float:
    """Maior sensibilidade + especificidade − 1 nas previsões fora da dobra do treino."""
    fpr, tpr, limiares = roc_curve(y, probabilidades)
    validos = np.isfinite(limiares)
    indice = int(np.argmax((tpr - fpr)[validos]))
    return float(np.clip(limiares[validos][indice], 0.0, 1.0))


@dataclass
class ModeloAjustado:
    variaveis: tuple[str, ...]
    c: float
    pipeline: Pipeline | None  # None quando não sobra variável: prevê a proporção do treino
    proporcao_treino: float

    def prever(self, x: np.ndarray) -> np.ndarray:
        if self.pipeline is None:
            return np.full(len(x), self.proporcao_treino)
        return self.pipeline.predict_proba(x)[:, 1]


def ajustar(x: np.ndarray, y: np.ndarray, variaveis: tuple[str, ...], c: float) -> ModeloAjustado:
    if not variaveis:
        return ModeloAjustado(variaveis, c, None, float(y.mean()))
    return ModeloAjustado(variaveis, c, construir_modelo(c).fit(x, y), float(y.mean()))


def coeficientes(modelo: ModeloAjustado) -> dict:
    """Coeficientes na escala padronizada e na original (o que um serviço aplicaria direto)."""
    if modelo.pipeline is None:
        logit = math.log(modelo.proporcao_treino / (1 - modelo.proporcao_treino))
        return {'intercepto': logit, 'coeficientes': {}, 'padronizados': {}, 'razao_de_chances_por_unidade': {}}
    escala: StandardScaler = modelo.pipeline[0]
    regressao: LogisticRegression = modelo.pipeline[1]
    beta = regressao.coef_[0]
    originais = beta / escala.scale_
    intercepto = float(regressao.intercept_[0] - np.sum(beta * escala.mean_ / escala.scale_))
    return {
        'intercepto': intercepto,
        'coeficientes': {v: float(b) for v, b in zip(modelo.variaveis, originais, strict=True)},
        'padronizados': {v: float(b) for v, b in zip(modelo.variaveis, beta, strict=True)},
        'razao_de_chances_por_unidade': {
            v: float(math.exp(b)) for v, b in zip(modelo.variaveis, originais, strict=True)
        },
        'padronizacao': {
            'media': {v: float(m) for v, m in zip(modelo.variaveis, escala.mean_, strict=True)},
            'desvio': {v: float(s) for v, s in zip(modelo.variaveis, escala.scale_, strict=True)},
        },
    }


# 4 - Métricas ------------------------------------------------------------------------


def wilson(acertos: int, total: int) -> list[float] | None:
    """IC 95% de Wilson para uma proporção (método de escore; Newcombe 1998)."""
    if total == 0:
        return None
    p = acertos / total
    denominador = 1 + Z_95**2 / total
    centro = (p + Z_95**2 / (2 * total)) / denominador
    meia = Z_95 * math.sqrt(p * (1 - p) / total + Z_95**2 / (4 * total**2)) / denominador
    return [max(0.0, centro - meia), min(1.0, centro + meia)]


def preditivos(sensibilidade: float, especificidade: float, prevalencia: float) -> tuple[float, float]:
    """VPP e VPN pelo teorema de Bayes, na prevalência declarada (não na da amostra)."""
    vp = sensibilidade * prevalencia
    fp = (1 - especificidade) * (1 - prevalencia)
    vn = especificidade * (1 - prevalencia)
    fn = (1 - sensibilidade) * prevalencia
    vpp = vp / (vp + fp) if vp + fp > 0 else float('nan')
    vpn = vn / (vn + fn) if vn + fn > 0 else float('nan')
    return vpp, vpn


def _logistica(x: np.ndarray, y: np.ndarray, deslocamento: np.ndarray, iteracoes: int = 100) -> np.ndarray:
    """Máxima verossimilhança por Newton, sem penalidade (só para a calibração)."""
    beta = np.zeros(x.shape[1])
    for _ in range(iteracoes):
        p = expit(deslocamento + x @ beta)
        hessiana = x.T @ (x * (p * (1 - p))[:, None]) + 1e-9 * np.eye(x.shape[1])
        passo = np.linalg.solve(hessiana, x.T @ (y - p))
        beta += passo
        if np.max(np.abs(passo)) < 1e-10:
            break
    return beta


def calibracao(y: np.ndarray, probabilidades: np.ndarray) -> dict[str, float]:
    """Intercepto (alvo 0) e inclinação (alvo 1) de calibração; Van Calster et al. 2019."""
    p = np.clip(probabilidades, 1e-6, 1 - 1e-6)
    lp = np.log(p / (1 - p))
    um = np.ones((len(y), 1))
    intercepto = _logistica(um, y.astype(np.float64), lp)[0]
    inclinacao = _logistica(np.column_stack([um, lp]), y.astype(np.float64), np.zeros(len(y)))[1]
    return {
        'intercepto': float(intercepto),
        'inclinacao': float(inclinacao),
        'brier': float(brier_score_loss(y, probabilidades)),
    }


def _pontuais(y: np.ndarray, prob: np.ndarray, corte: float, prevalencias: tuple[float, ...]) -> dict[str, float]:
    previsto = prob >= corte
    sens = float(previsto[y == 1].mean())
    esp = float((~previsto[y == 0]).mean())
    valores = {
        'auc_roc': float(roc_auc_score(y, prob)),
        'auc_pr': float(average_precision_score(y, prob)),
        'sensibilidade': sens,
        'especificidade': esp,
    }
    for prev in prevalencias:
        vpp, vpn = preditivos(sens, esp, prev)
        valores[f'vpp@{prev:.4f}'] = vpp
        valores[f'vpn@{prev:.4f}'] = vpn
    return valores


def avaliar(
    y: np.ndarray,
    prob: np.ndarray,
    corte: float,
    prevalencias: tuple[float, ...],
    semente: int,
    reamostragens: int = 2000,
    prob_referencia: np.ndarray | None = None,
) -> dict:
    """Métricas do protocolo (ADR-0002) no conjunto dado, com IC 95%.

    Sensibilidade e especificidade: Wilson. AUCs, VPP/VPN e a diferença de AUC para
    o modelo de referência: bootstrap estratificado por grupo (percentis 2,5 e 97,5).
    """
    if len(np.unique(y)) < 2:
        raise ValueError('O conjunto avaliado precisa ter participantes dos dois grupos.')
    pontos = _pontuais(y, prob, corte, prevalencias)
    previsto = prob >= corte
    vp, fn = int(previsto[y == 1].sum()), int((~previsto[y == 1]).sum())
    vn, fp = int((~previsto[y == 0]).sum()), int(previsto[y == 0].sum())
    if prob_referencia is not None:
        pontos['auc_roc_covariaveis'] = float(roc_auc_score(y, prob_referencia))
        pontos['ganho_auc_roc'] = pontos['auc_roc'] - pontos['auc_roc_covariaveis']

    rng = np.random.default_rng(semente)
    casos, controles = np.flatnonzero(y == 1), np.flatnonzero(y == 0)
    amostras: dict[str, list[float]] = {k: [] for k in pontos if k not in ('sensibilidade', 'especificidade')}
    for _ in range(reamostragens):
        indices = np.concatenate([rng.choice(casos, len(casos)), rng.choice(controles, len(controles))])
        reamostra = _pontuais(y[indices], prob[indices], corte, prevalencias)
        if prob_referencia is not None:
            reamostra['auc_roc_covariaveis'] = float(roc_auc_score(y[indices], prob_referencia[indices]))
            reamostra['ganho_auc_roc'] = reamostra['auc_roc'] - reamostra['auc_roc_covariaveis']
        for chave in amostras:
            amostras[chave].append(reamostra[chave])

    def ic(chave: str) -> list[float] | None:
        valores = np.array(amostras[chave])
        valores = valores[np.isfinite(valores)]
        return [float(np.percentile(valores, 2.5)), float(np.percentile(valores, 97.5))] if len(valores) else None

    resultado = {
        'participantes': len(y),
        'tea': int(y.sum()),
        'controles': int(len(y) - y.sum()),
        'ponto_de_corte': corte,
        'matriz_confusao': {'vp': vp, 'fn': fn, 'vn': vn, 'fp': fp},
        'sensibilidade': {'valor': pontos['sensibilidade'], 'ic95': wilson(vp, vp + fn)},
        'especificidade': {'valor': pontos['especificidade'], 'ic95': wilson(vn, vn + fp)},
        'calibracao': calibracao(y, prob),
        'reamostragens_bootstrap': reamostragens,
    }
    for chave in amostras:
        resultado[chave] = {'valor': pontos[chave], 'ic95': ic(chave)}
    return resultado


def tamanho_amostral(y_treino: np.ndarray, parametros_candidatos: int) -> dict[str, float]:
    """Eventos por parâmetro candidato e margem de erro da proporção (Riley et al. 2020, critério 1).

    Não substitui o cálculo completo (encolhimento e otimismo do R²), feito com o
    pmsampsize a partir do desempenho esperado.
    """
    n = len(y_treino)
    menor_grupo = int(np.bincount(y_treino, minlength=2).min())
    fracao = float(y_treino.mean())
    return {
        'participantes_treino': n,
        'menor_grupo_treino': menor_grupo,
        'parametros_candidatos': parametros_candidatos,
        'eventos_por_parametro_candidato': menor_grupo / parametros_candidatos,
        'margem_erro_proporcao_tea': Z_95 * math.sqrt(fracao * (1 - fracao) / n),
    }


# 5 - Gráficos ------------------------------------------------------------------------


def graficos(y: np.ndarray, prob: np.ndarray, prob_cov: np.ndarray, pasta: Path) -> None:
    pasta.mkdir(parents=True, exist_ok=True)
    plt.figure(figsize=(5, 5))
    for nome, valores, estilo in (('indicadores + covariáveis', prob, '-'), ('só sexo e idade', prob_cov, '--')):
        fpr, tpr, _ = roc_curve(y, valores)
        plt.plot(fpr, tpr, estilo, label=f'{nome} (AUC {roc_auc_score(y, valores):.2f})')
    plt.plot([0, 1], [0, 1], ':', color='grey')
    plt.xlabel('1 − especificidade')
    plt.ylabel('Sensibilidade')
    plt.title('Curva ROC no teste')
    plt.legend(loc='lower right')
    plt.tight_layout()
    plt.savefig(pasta / 'roc.svg')
    plt.close()

    # Calibração por faixas (quantis): com amostra pequena, só uma leitura grosseira.
    faixas = min(5, max(2, len(y) // 10))
    limites = np.unique(np.quantile(prob, np.linspace(0, 1, faixas + 1)))
    indice = np.clip(np.digitize(prob, limites[1:-1]), 0, len(limites) - 2)
    previstos = [prob[indice == i].mean() for i in range(len(limites) - 1) if (indice == i).any()]
    observados = [y[indice == i].mean() for i in range(len(limites) - 1) if (indice == i).any()]
    plt.figure(figsize=(5, 5))
    plt.plot([0, 1], [0, 1], ':', color='grey', label='calibração perfeita')
    plt.plot(previstos, observados, 'o-', label='faixas de probabilidade prevista')
    plt.xlabel('Probabilidade prevista')
    plt.ylabel('Proporção observada de TEA')
    plt.title('Calibração no teste')
    plt.legend(loc='upper left')
    plt.tight_layout()
    plt.savefig(pasta / 'calibracao.svg')
    plt.close()


# 6 - Execução ------------------------------------------------------------------------


def dentro_do_repositorio(caminho: Path) -> bool:
    try:
        caminho.resolve().relative_to(RAIZ_REPOSITORIO)
        return True
    except ValueError:
        return False


def argumentos(lista: list[str] | None = None) -> argparse.Namespace:
    dados_padrao = Path(os.environ.get('TCC_DADOS_DIR', 'dados_locais')) / 'sinteticos_etapa2'
    p = argparse.ArgumentParser(description='Treina o escore da Etapa 2 (ADR-0002, ADR-0011).')
    p.add_argument('--dados', type=Path, default=dados_padrao, help='pasta com participantes.csv e padroes.csv')
    p.add_argument('--origem', choices=['SINTETICO', 'REAL'], required=True)
    p.add_argument('--origem-padroes', choices=['ANOTACAO', 'ETAPA1'], required=True)
    p.add_argument('--versao-etapa1', default=None, help='versão da CNN que gerou os padrões (com ETAPA1)')
    p.add_argument('--divisao-etapa1', type=Path, default=None, help='divisao.json do treino da Etapa 1')
    p.add_argument('--variaveis', choices=sorted(CONJUNTOS_VARIAVEIS), default='minimo')
    p.add_argument('--fracao-teste', type=float, default=0.25)
    p.add_argument('--prevalencias', type=float, nargs='+', default=list(PREVALENCIAS_PADRAO))
    p.add_argument('--reamostragens', type=int, default=2000)
    p.add_argument('--semente', type=int, default=42)
    p.add_argument('--versao', default=None, help='padrão: AAAA-MM-DD_escore_<origem>_v1')
    p.add_argument('--saida-local', type=Path, default=None, help='padrão: <dados>/saidas/<versao>')
    p.add_argument('--destino-modelos', type=Path, default=RAIZ_REPOSITORIO / 'treino' / 'modelos')
    args = p.parse_args(lista)
    if not 0 < args.fracao_teste < 0.5:
        p.error('--fracao-teste deve ficar entre 0 e 0,5.')
    if any(not 0 < v < 1 for v in args.prevalencias):
        p.error('--prevalencias devem ficar entre 0 e 1.')
    if args.origem_padroes == 'ETAPA1' and (args.versao_etapa1 is None or args.divisao_etapa1 is None):
        p.error('Com --origem-padroes ETAPA1, informe --versao-etapa1 e --divisao-etapa1 (ADR-0002).')
    return args


def principal(lista: list[str] | None = None) -> Path:
    args = argumentos(lista)
    versao = args.versao or f'{date.today():%Y-%m-%d}_escore_{args.origem.lower()}_v1'
    if not VERSAO_VALIDA.match(versao):
        raise SystemExit(f'Versão inválida: {versao!r} (use letras, números, "_", "-" e ".").')
    if dentro_do_repositorio(args.dados):
        raise SystemExit('Os dados ficam fora do repositório (CLAUDE.md, Regra 4).')
    pasta_versao = args.destino_modelos / ('sinteticos' if args.origem == 'SINTETICO' else 'reais') / versao
    if pasta_versao.exists():
        raise SystemExit(f'{pasta_versao} já existe: use outra --versao.')
    prevalencias = tuple(args.prevalencias)

    # 1 - Leitura -----------------------------------------------------------------------
    try:
        linhas = ler_csv(args.dados / 'participantes.csv', ('participante', 'sexo', 'idade_meses', 'grupo'))
        participantes, excluidos = montar_participantes(linhas, ler_padroes(args.dados / 'padroes.csv'))
    except (OSError, ValueError) as erro:
        raise SystemExit(str(erro)) from erro

    # 2 - Divisão -----------------------------------------------------------------------
    fora_do_teste: set[str] = set()
    if args.divisao_etapa1 is not None:
        divisao_cnn = json.loads(args.divisao_etapa1.read_text(encoding='utf-8'))
        fora_do_teste = set(divisao_cnn.get('treino', [])) | set(divisao_cnn.get('validacao', []))
    try:
        treino, teste = dividir(participantes, args.fracao_teste, args.semente, fora_do_teste)
    except ValueError as erro:
        raise SystemExit(str(erro)) from erro
    saida_local = args.saida_local or args.dados / 'saidas' / versao
    if dentro_do_repositorio(saida_local):
        raise SystemExit('A saída local tem códigos de participante: fica fora do repositório (Regra 4).')
    saida_local.mkdir(parents=True, exist_ok=True)
    (saida_local / 'divisao.json').write_text(
        json.dumps({'treino': sorted(p.codigo for p in treino), 'teste': sorted(p.codigo for p in teste)}, indent=1),
        encoding='utf-8',
    )

    # 3 - Variáveis (as constantes no treino saem: não há o que estimar) -------------------
    candidatas = CONJUNTOS_VARIAVEIS[args.variaveis]
    x_todas, y_treino = matriz(treino, candidatas)
    constantes = [v for j, v in enumerate(candidatas) if np.ptp(x_todas[:, j]) == 0]
    variaveis = tuple(v for v in candidatas if v not in constantes)
    covariaveis = tuple(v for v in COVARIAVEIS if v in variaveis)
    x_treino, _ = matriz(treino, variaveis)
    x_teste, y_teste = matriz(teste, variaveis)
    x_cov_treino, _ = matriz(treino, covariaveis)
    x_cov_teste, _ = matriz(teste, covariaveis)
    for v in constantes:
        print(f'Aviso: {v} é constante no treino e foi retirada do modelo.')
    amostra = tamanho_amostral(y_treino, len(candidatas))
    if amostra['eventos_por_parametro_candidato'] < 10:
        print(
            f'Aviso: {amostra["eventos_por_parametro_candidato"]:.1f} participantes do menor grupo por parâmetro '
            'candidato. Riley et al. (2020) mostram que a regra dos 10 é simplista: calcule o tamanho mínimo com o '
            'pmsampsize antes de interpretar o resultado.'
        )

    # 4 - Treino: C e ponto de corte por validação cruzada no treino ------------------------
    with warnings.catch_warnings():
        warnings.simplefilter('ignore', ConvergenceWarning)
        try:
            c, fora_da_dobra, perdas = escolher_c(x_treino, y_treino, args.semente)
            c_cov, fora_da_dobra_cov, _ = escolher_c(x_cov_treino, y_treino, args.semente)
        except ValueError as erro:
            raise SystemExit(str(erro)) from erro
        modelo = ajustar(x_treino, y_treino, variaveis, c)
        modelo_cov = ajustar(x_cov_treino, y_treino, covariaveis, c_cov)
    corte = ponto_de_corte_youden(y_treino, fora_da_dobra)

    # 5 - Avaliação no teste (usado uma única vez) ------------------------------------------
    prob_teste = modelo.prever(x_teste)
    prob_cov_teste = modelo_cov.prever(x_cov_teste)
    metricas_teste = avaliar(y_teste, prob_teste, corte, prevalencias, args.semente, args.reamostragens, prob_cov_teste)
    metricas_cv = avaliar(
        y_treino, fora_da_dobra, corte, prevalencias, args.semente, args.reamostragens, fora_da_dobra_cov
    )

    idade = {
        g: {
            'media': float(np.mean([p.variaveis['idade_meses'] for p in treino if p.grupo == i])),
            'desvio': float(np.std([p.variaveis['idade_meses'] for p in treino if p.grupo == i])),
        }
        for i, g in enumerate(GRUPOS)
    }
    print(json.dumps({'teste': metricas_teste, 'amostra': amostra}, indent=1, ensure_ascii=False))

    # 6 - Gravação ----------------------------------------------------------------------
    pasta_versao.mkdir(parents=True)
    graficos(y_teste, prob_teste, prob_cov_teste, pasta_versao / 'graficos')
    with (saida_local / 'previsoes_teste.csv').open('w', newline='', encoding='utf-8') as arquivo:
        escritor = csv.writer(arquivo)  # tem códigos de participante: fica SÓ na pasta local
        escritor.writerow(['participante', 'grupo', 'probabilidade', 'probabilidade_covariaveis'])
        for p, pr, pc in zip(teste, prob_teste, prob_cov_teste, strict=True):
            escritor.writerow([p.codigo, GRUPOS[p.grupo], f'{pr:.6f}', f'{pc:.6f}'])

    fracao_tea = float(y_treino.mean())
    coefs = {
        'versao': versao,
        'origem_dados': args.origem,
        'validado': False,  # só uma decisão registrada em ADR muda isto (RN10)
        'variaveis': list(variaveis),
        'codificacao': {'sexo_feminino': '1 = FEMININO, 0 = MASCULINO', 'saida': 'probabilidade de grupo TEA'},
        **coeficientes(modelo),
        'ponto_de_corte': corte,
        'fracao_tea_no_treino': fracao_tea,
        'correcao_de_prevalencia': (
            'Num caso-controle o intercepto reflete a fração de casos da amostra. Para uma prevalência τ: '
            'intercepto − ln(((1 − τ)/τ) · (f/(1 − f))), com f = fracao_tea_no_treino (King & Zeng 2001).'
        ),
    }
    config = {
        'versao': versao,
        'etapa': 2,
        'origem_dados': args.origem,
        'origem_padroes': args.origem_padroes,
        'versao_etapa1': args.versao_etapa1,
        'conjunto_variaveis': args.variaveis,
        'variaveis_candidatas': list(candidatas),
        'variaveis_retiradas_por_serem_constantes': constantes,
        'modelo': 'regressão logística L2 (scikit-learn), variáveis padronizadas',
        'grade_c': list(GRADE_C),
        'c_escolhido': c,
        'log_loss_por_c': perdas,
        'dobras_validacao_cruzada': dobras_para(y_treino),
        'ponto_de_corte': {'valor': corte, 'metodo': 'Youden nas previsões fora da dobra do treino'},
        'fracao_teste': args.fracao_teste,
        'teste_exclui_treino_e_validacao_da_etapa1': args.divisao_etapa1 is not None,
        'prevalencias': list(prevalencias),
        'semente': args.semente,
        'divisao': {
            nome: {'participantes': len(lista), 'tea': sum(p.grupo for p in lista)}
            for nome, lista in (('treino', treino), ('teste', teste))
        },
        'excluidos': excluidos,
        'versoes': {'python': platform.python_version(), 'scikit_learn': sklearn.__version__, 'numpy': np.__version__},
        'treinado_em': date.today().isoformat(),
    }
    metricas = {
        'teste': metricas_teste,
        'validacao_cruzada_treino': metricas_cv,
        'amostra': {**amostra, 'idade_meses_por_grupo_treino': idade},
    }
    for nome, conteudo in (('coeficientes.json', coefs), ('config.json', config), ('metricas.json', metricas)):
        (pasta_versao / nome).write_text(json.dumps(conteudo, indent=1, ensure_ascii=False), encoding='utf-8')
    (pasta_versao / 'NOTAS.md').write_text(notas(versao, args, metricas_teste, amostra, constantes), encoding='utf-8')

    print(f'\nVersão {versao} gravada em {pasta_versao}')
    print('O escore NÃO é exibido no sistema até uma validação registrada em ADR (RN10).')
    return pasta_versao


def notas(versao: str, args: argparse.Namespace, teste: dict, amostra: dict, constantes: list[str]) -> str:
    auc, ganho = teste['auc_roc'], teste['ganho_auc_roc']
    linhas = [
        f'# {versao}',
        '',
        f'- Origem dos dados: **{args.origem}**'
        + (
            ': o grupo TEA/controle foi inventado (por exemplo, por `treino/scripts/sinteticos_etapa2.py`). '
            'As métricas conferem o código e não dizem nada sobre TEA.'
            if args.origem == 'SINTETICO'
            else '.'
        ),
        f'- Padrões dos dedos: {args.origem_padroes}'
        + (f' (Etapa 1 `{args.versao_etapa1}`).' if args.origem_padroes == 'ETAPA1' else ' (anotação manual).'),
        f'- Variáveis: conjunto `{args.variaveis}`'
        + (f'; retiradas por serem constantes no treino: {", ".join(constantes)}.' if constantes else '.'),
        f'- Teste: {teste["participantes"]} participantes ({teste["tea"]} TEA). '
        f'ROC-AUC {auc["valor"]:.3f} (IC 95% {auc["ic95"][0]:.3f}–{auc["ic95"][1]:.3f}); '
        f'ganho sobre sexo e idade {ganho["valor"]:+.3f} (IC 95% {ganho["ic95"][0]:+.3f} a {ganho["ic95"][1]:+.3f}).',
        f'- Eventos por parâmetro candidato: {amostra["eventos_por_parametro_candidato"]:.1f}.',
        '- Escore **não validado**: não aparece no sistema (RN10).',
        '- O que mudou em relação à versão anterior: (preencher).',
        '',
    ]
    return '\n'.join(linhas)


if __name__ == '__main__':
    principal()
