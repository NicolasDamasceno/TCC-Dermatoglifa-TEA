"""Serviço de IA (ADR-0001). Único cliente: a API .NET, pela rede interna.

Recebe as 10 imagens de uma sessão (sem nome nem identificador do paciente),
classifica o padrão de cada dedo (Etapa 1), converte as alças para ulnar/radial,
calcula os indicadores e devolve tudo no contrato do ADR-0001. Não acessa banco
e não grava imagens: elas existem só na memória durante a requisição.
"""

from __future__ import annotations

import logging
import time
from collections.abc import AsyncIterator, Callable
from contextlib import asynccontextmanager
from dataclasses import asdict
from typing import Any

import numpy as np
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
from starlette.concurrency import run_in_threadpool
from starlette.datastructures import UploadFile
from starlette.exceptions import HTTPException
from starlette.formparsers import MultiPartException

from dermatoglifia_ia.dominio import ORDEM_COLETA, ConvencaoAlca, Posicao, calcular_indicadores, converter
from dermatoglifia_ia.modelo import ErroModelo, ModeloCarregado, escolher_modelo
from dermatoglifia_ia.preprocessamento import ImagemInvalida, decodificar_png, redimensionar

from .configuracao import Configuracao
from .qualidade import alerta_qualidade

log = logging.getLogger('servico')

SEXOS = {'MASCULINO', 'FEMININO'}
IDADE_MAXIMA_MESES = 120 * 12
# A API limita cada imagem a 5 MB; uma folga cobre o cabeçalho de cada parte.
TAMANHO_MAXIMO_PARTE = 5 * 1024 * 1024 + 64 * 1024
CAMPOS_ESPERADOS = {p.nome_arquivo for p in ORDEM_COLETA} | {'sexo', 'idade_meses'}


class RequisicaoInvalida(ValueError):
    pass


def _erro(status: int, mensagem: str) -> JSONResponse:
    return JSONResponse(status_code=status, content={'detail': mensagem})


def criar_app(
    configuracao: Configuracao | None = None,
    carregar: Callable[[Configuracao], ModeloCarregado] | None = None,
) -> FastAPI:
    config = configuracao or Configuracao.do_ambiente()
    carregar_modelo = carregar or (lambda c: escolher_modelo(c.pasta_producao, c.pasta_demo))
    estado: dict[str, Any] = {'modelo': None, 'motivo': 'Modelo ainda não carregado.'}

    @asynccontextmanager
    async def ciclo_de_vida(_: FastAPI) -> AsyncIterator[None]:
        try:
            modelo = carregar_modelo(config)
            estado.update(modelo=modelo, motivo=None)
            log.info('Modelo %s carregado (%s).', modelo.config.versao, modelo.origem)
        except ErroModelo as erro:
            # O serviço sobe mesmo assim: /saude explica o motivo e /inferencia responde 503.
            estado.update(modelo=None, motivo=str(erro))
            log.error('Sem modelo: %s', erro)
        yield

    app = FastAPI(
        title='Dermatoglifia TEA: Serviço de IA',
        version='1.0.0',
        lifespan=ciclo_de_vida,
        docs_url=None,  # rede interna, um só cliente: sem páginas de documentação expostas
        redoc_url=None,
        openapi_url=None,
    )

    @app.get('/saude')
    def saude() -> JSONResponse:
        modelo: ModeloCarregado | None = estado['modelo']
        if modelo is None:
            return JSONResponse(status_code=503, content={'status': 'INDISPONIVEL', 'motivo': estado['motivo']})
        return JSONResponse(
            content={
                'status': 'OK',
                'versao_modelo': modelo.config.versao,
                'origem': modelo.origem,
                'origem_dados': modelo.config.origem_dados,
                'sha256': modelo.sha256,
                'tamanho_imagem': list(modelo.config.tamanho_imagem),
                'convencao_alca': config.convencao_alca,
            }
        )

    @app.post('/inferencia')
    async def inferencia(request: Request) -> JSONResponse:
        modelo: ModeloCarregado | None = estado['modelo']
        if modelo is None:
            return _erro(503, f'Serviço sem modelo: {estado["motivo"]}')

        inicio = time.perf_counter()
        try:
            imagens, sexo, idade_meses = await _ler_requisicao(request)
        except RequisicaoInvalida as erro:
            return _erro(422, str(erro))

        # Sexo e idade são validados, mas a Etapa 1 não os usa: entram só na Etapa 2,
        # ainda não validada (escore_risco = null, RN10).
        del sexo, idade_meses
        try:
            # Decodificar e rodar a CNN é trabalho de CPU: fora do laço de eventos,
            # para /saude continuar respondendo durante uma inferência.
            resposta = await run_in_threadpool(_analisar, modelo, imagens, config)
        except ImagemInvalida as erro:
            return _erro(422, str(erro))

        log.info(
            'Inferência concluída em %.0f ms com o modelo %s.',
            (time.perf_counter() - inicio) * 1000,
            modelo.config.versao,
        )
        return JSONResponse(content=resposta)

    return app


def _analisar(modelo: ModeloCarregado, imagens: dict[Posicao, bytes], config: Configuracao) -> dict[str, Any]:
    """Pré-processa (mesmo código do treino), classifica, converte e calcula os indicadores."""
    cfg = modelo.config
    originais: dict[Posicao, np.ndarray] = {}
    lote = []
    for posicao in ORDEM_COLETA:
        try:
            imagem = decodificar_png(imagens[posicao])
        except ImagemInvalida as erro:
            raise ImagemInvalida(f'{posicao.nome_arquivo}: {erro}') from erro
        originais[posicao] = imagem.numpy()
        lote.append(redimensionar(imagem, cfg.tamanho_imagem, cfg.ajuste_proporcao, cfg.interpolacao).numpy())

    probabilidades = modelo.prever(np.stack(lote).astype(np.float32))
    digitais, padroes = [], {}
    for posicao, linha in zip(ORDEM_COLETA, probabilidades, strict=True):
        indice = int(np.argmax(linha))
        classe = cfg.classes[indice]
        padrao = converter(classe, posicao.mao, ConvencaoAlca(config.convencao_alca))
        confianca = round(float(linha[indice]), 4)
        padroes[posicao] = padrao
        digitais.append(
            {
                'mao': posicao.mao,
                'dedo': posicao.dedo,
                'classe_imagem': classe,
                'padrao': padrao,
                'confianca': confianca,
                'probabilidades': {c: round(float(linha[i]), 4) for i, c in enumerate(cfg.classes)},
                'alerta_qualidade': alerta_qualidade(
                    originais[posicao], confianca, config.limiar_confianca, config.limiar_contraste
                ),
            }
        )

    return {
        'versao_modelo': cfg.versao,
        'digitais': digitais,
        'indicadores': asdict(calcular_indicadores(padroes)),
        'escore_risco': None,
    }


async def _ler_requisicao(request: Request) -> tuple[dict[Posicao, bytes], str, int]:
    """Valida o multipart do contrato: os 10 dedos em PNG, sexo e idade, e nada mais."""
    if not request.headers.get('content-type', '').startswith('multipart/form-data'):
        raise RequisicaoInvalida('Envie multipart/form-data.')
    try:
        # Folga de dois campos de texto para nomear o que veio a mais; além disso, o
        # próprio Starlette recusa (limites contra corpos abusivos).
        formulario = await request.form(max_files=len(ORDEM_COLETA), max_fields=4, max_part_size=TAMANHO_MAXIMO_PARTE)
    except MultiPartException as erro:
        raise RequisicaoInvalida(f'Formulário inválido: {erro.message}') from erro
    except HTTPException as erro:
        raise RequisicaoInvalida(f'Formulário inválido: {erro.detail}') from erro

    nomes = list(formulario.keys())
    extras = set(nomes) - CAMPOS_ESPERADOS
    if extras:
        raise RequisicaoInvalida(f'Campos não previstos no contrato: {", ".join(sorted(extras))}.')
    if len(nomes) != len(set(nomes)):
        raise RequisicaoInvalida('Campo repetido.')

    imagens: dict[Posicao, bytes] = {}
    for posicao in ORDEM_COLETA:
        parte = formulario.get(posicao.nome_arquivo)
        if not isinstance(parte, UploadFile):
            raise RequisicaoInvalida(f'Falta a imagem {posicao.nome_arquivo}.')
        if parte.content_type != 'image/png':
            raise RequisicaoInvalida(f'{posicao.nome_arquivo}: esperado image/png.')
        imagens[posicao] = await parte.read()

    sexo = formulario.get('sexo')
    if not isinstance(sexo, str) or sexo not in SEXOS:
        raise RequisicaoInvalida('sexo deve ser MASCULINO ou FEMININO.')

    idade = formulario.get('idade_meses')
    if not isinstance(idade, str) or not idade.isdigit() or not 0 <= int(idade) <= IDADE_MAXIMA_MESES:
        raise RequisicaoInvalida('idade_meses deve ser um inteiro de 0 a 1440.')

    return imagens, sexo, int(idade)


# Fica disponível para o uvicorn: `uvicorn servico.app:app`.
app = criar_app()

__all__ = ['app', 'criar_app']
