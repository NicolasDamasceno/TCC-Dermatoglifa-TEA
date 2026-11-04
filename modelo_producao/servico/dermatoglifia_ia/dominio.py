"""Vocabulário do domínio e regras determinísticas do ADR-0002.

Espelha o domínio do back-end (.NET), que confere cada resposta do serviço:
`TabelaConversaoAlca` e `IndicadoresDermatoglificos`. Mudar uma regra aqui exige
mudar lá também, senão a API recusa as respostas.
"""

from __future__ import annotations

from collections.abc import Mapping
from dataclasses import dataclass
from enum import StrEnum


class Mao(StrEnum):
    DIREITA = 'DIREITA'
    ESQUERDA = 'ESQUERDA'


class Dedo(StrEnum):
    POLEGAR = 'POLEGAR'
    INDICADOR = 'INDICADOR'
    MEDIO = 'MEDIO'
    ANELAR = 'ANELAR'
    MINIMO = 'MINIMO'


class ClasseImagem(StrEnum):
    """O que a CNN (Etapa 1) prevê, no espaço da imagem, sem saber a mão."""

    ARCO = 'ARCO'
    ARCO_TENDIDO = 'ARCO_TENDIDO'
    ALCA_ESQUERDA = 'ALCA_ESQUERDA'
    ALCA_DIREITA = 'ALCA_DIREITA'
    VERTICILO = 'VERTICILO'


class Padrao(StrEnum):
    """Padrão já convertido para ulnar/radial pela mão."""

    ARCO = 'ARCO'
    ARCO_TENDIDO = 'ARCO_TENDIDO'
    ALCA_ULNAR = 'ALCA_ULNAR'
    ALCA_RADIAL = 'ALCA_RADIAL'
    VERTICILO = 'VERTICILO'


class ConvencaoAlca(StrEnum):
    """Como o dispositivo de captura orienta a imagem (ADR-0002, ADR-0004).

    IMPRESSAO: a imagem é a impressão como sai no papel (alça que abre para a
    direita é ulnar na mão direita). ESPELHADA: o leitor entrega a imagem
    espelhada. Precisa ser a mesma configurada na API (`Inferencia:ConvencaoAlca`).
    """

    IMPRESSAO = 'IMPRESSAO'
    ESPELHADA = 'ESPELHADA'


@dataclass(frozen=True, order=True)
class Posicao:
    mao: Mao
    dedo: Dedo

    @property
    def nome_arquivo(self) -> str:
        """Nome do campo no multipart: `DIREITA_POLEGAR` (ADR-0001)."""
        return f'{self.mao}_{self.dedo}'

    @classmethod
    def do_nome(cls, nome: str) -> Posicao:
        mao, separador, dedo = nome.partition('_')
        if not separador:
            raise ValueError(f'Posição inválida: {nome!r}.')
        return cls(Mao(mao), Dedo(dedo))


# Ordem da ficha decadactilar: direita do polegar ao mínimo, depois a esquerda.
ORDEM_COLETA: tuple[Posicao, ...] = tuple(Posicao(mao, dedo) for mao in Mao for dedo in Dedo)


def converter(classe: ClasseImagem, mao: Mao, convencao: ConvencaoAlca = ConvencaoAlca.IMPRESSAO) -> Padrao:
    """Classe no espaço da imagem → padrão ulnar/radial, pela mão (ADR-0002)."""
    match classe:
        case ClasseImagem.ARCO:
            return Padrao.ARCO
        case ClasseImagem.ARCO_TENDIDO:
            return Padrao.ARCO_TENDIDO
        case ClasseImagem.VERTICILO:
            return Padrao.VERTICILO
        case ClasseImagem.ALCA_DIREITA | ClasseImagem.ALCA_ESQUERDA:
            abre_para_direita = classe is ClasseImagem.ALCA_DIREITA
            ulnar_na_impressao = abre_para_direita == (mao is Mao.DIREITA)
            ulnar = ulnar_na_impressao == (convencao is ConvencaoAlca.IMPRESSAO)
            return Padrao.ALCA_ULNAR if ulnar else Padrao.ALCA_RADIAL


@dataclass(frozen=True)
class Indicadores:
    qtd_arcos: int
    qtd_arcos_tendidos: int
    qtd_alcas_ulnares: int
    qtd_alcas_radiais: int
    qtd_verticilos: int
    discordancia_bilateral: int
    discordancia_bilateral_com_polegar: int


def calcular_indicadores(padroes: Mapping[Posicao, Padrao]) -> Indicadores:
    """Contagens por padrão e discordância bilateral.

    `discordancia_bilateral`: pares homólogos (indicador, médio, anelar e mínimo)
    com padrões diferentes entre as mãos, de 0 a 4, polegar excluído, como em
    de Bruin et al. (2014); `discordancia_bilateral_com_polegar`: de 0 a 5.
    """
    if set(padroes) != set(ORDEM_COLETA):
        raise ValueError('Os indicadores exigem o padrão dos 10 dedos.')

    valores = list(padroes.values())

    def discordantes(dedos: list[Dedo]) -> int:
        return sum(padroes[Posicao(Mao.DIREITA, d)] != padroes[Posicao(Mao.ESQUERDA, d)] for d in dedos)

    sem_polegar = [d for d in Dedo if d is not Dedo.POLEGAR]
    return Indicadores(
        qtd_arcos=valores.count(Padrao.ARCO),
        qtd_arcos_tendidos=valores.count(Padrao.ARCO_TENDIDO),
        qtd_alcas_ulnares=valores.count(Padrao.ALCA_ULNAR),
        qtd_alcas_radiais=valores.count(Padrao.ALCA_RADIAL),
        qtd_verticilos=valores.count(Padrao.VERTICILO),
        discordancia_bilateral=discordantes(sem_polegar),
        discordancia_bilateral_com_polegar=discordantes(list(Dedo)),
    )
