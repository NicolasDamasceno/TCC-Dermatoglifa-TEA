"""Configuração por variáveis de ambiente (nada de caminho fixo, CLAUDE.md Regra 3)."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from dermatoglifia_ia.dominio import ConvencaoAlca

# modelo_producao/ (este arquivo está em modelo_producao/servico/servico/)
RAIZ_MODELOS = Path(__file__).resolve().parents[2]


@dataclass(frozen=True)
class Configuracao:
    pasta_producao: Path
    pasta_demo: Path
    convencao_alca: ConvencaoAlca
    limiar_confianca: float
    limiar_contraste: float

    @classmethod
    def do_ambiente(cls) -> Configuracao:
        ambiente = os.environ
        return cls(
            pasta_producao=Path(ambiente.get('DERMATOGLIFIA_MODELO_PRODUCAO', RAIZ_MODELOS / 'producao')),
            pasta_demo=Path(ambiente.get('DERMATOGLIFIA_MODELO_DEMO', RAIZ_MODELOS / 'demo')),
            convencao_alca=ConvencaoAlca(ambiente.get('DERMATOGLIFIA_CONVENCAO_ALCA', ConvencaoAlca.IMPRESSAO)),
            limiar_confianca=float(ambiente.get('DERMATOGLIFIA_LIMIAR_CONFIANCA', '0.5')),
            limiar_contraste=float(ambiente.get('DERMATOGLIFIA_LIMIAR_CONTRASTE', '10')),
        )
