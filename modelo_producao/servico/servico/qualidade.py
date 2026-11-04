"""Alerta de qualidade por dedo (UC07, fluxo 3a).

Heurística provisória, até a adoção do NFIQ 2 (ADR-0004, "Revisitar"). Marca o
dedo quando:
- a imagem é quase uniforme (desvio-padrão dos tons de cinza abaixo do limiar):
  arquivo em branco, dedo fora do leitor, exposição errada; ou
- a CNN ficou indecisa (probabilidade da classe prevista abaixo do limiar).

Os limiares são escolhas de engenharia, configuráveis, e não foram validados;
o alerta pede ao médico que confira o dedo, não afirma que a imagem é ruim.
"""

from __future__ import annotations

import numpy as np


def alerta_qualidade(imagem: np.ndarray, confianca: float, limiar_confianca: float, limiar_contraste: float) -> bool:
    return float(np.std(imagem)) < limiar_contraste or confianca < limiar_confianca
