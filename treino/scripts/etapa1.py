"""Etapa 1 — CNN que classifica o padrão de cada dedo (ADR-0002).

Segue a estrutura do `treino/referencia/modelo_arroz_keras.py`, das aulas de IA do
3º período, com as seções na mesma ordem e o mesmo vocabulário (CLAUDE.md, Regra 3).
O que mudou em relação à referência, e por quê:

- classes no espaço da imagem (ARCO, ARCO_TENDIDO, ALCA_ESQUERDA, ALCA_DIREITA,
  VERTICILO); a conversão para ulnar/radial é feita depois, pela mão;
- saída `softmax` (a referência usa `sigmoid` com `categorical_crossentropy`,
  combinação incorreta para classes exclusivas);
- imagens em cinza, com o pré-processamento do pacote `dermatoglifia_ia`, o mesmo
  do Serviço de IA (ADR-0001);
- sem `RandomFlip` no modelo: espelhar troca alça esquerda por direita. O
  espelhamento acontece no pipeline de dados (metade das imagens, sorteadas a cada
  lote), trocando o rótulo junto;
- rotação pequena (até ±20°), nunca 90° ou 180°;
- divisão treino/validação/teste por participante, nunca por imagem;
- matriz de confusão impressa e sensibilidade, especificidade e AUC por classe;
- caminhos por argumento ou variável de ambiente, nada fixo no Google Drive;
- estatísticas do BatchNormalization recalculadas antes de cada validação e no fim
  do treino (PreciseBN; Wu & Johnson, 2021), em vez da média móvel;
- treino em CPU (a RX 6600 não tem suporte oficial no ROCm; ADR-0002).

Uso típico (dados sintéticos, gera o modelo de demonstração):

    python treino/scripts/sinteticos.py --participantes 400
    python treino/scripts/etapa1.py --origem SINTETICO --promover-demo
"""

from __future__ import annotations

import argparse
import json
import os
import platform
import re
import shutil
from collections import Counter
from datetime import date, datetime
from pathlib import Path

os.environ.setdefault('TF_CPP_MIN_LOG_LEVEL', '2')

import keras
import matplotlib

matplotlib.use('Agg')  # só arquivos; nenhuma janela
import divisao as divisao_mod
import matplotlib.pyplot as plt
import numpy as np
import tensorflow as tf
from dermatoglifia_ia.aumento import TROCA_NO_ESPELHO, espelhar_aleatoriamente
from dermatoglifia_ia.dominio import ClasseImagem
from dermatoglifia_ia.modelo import sha256_arquivo
from dermatoglifia_ia.preprocessamento import AjusteProporcao, argumentos_dataset
from keras.callbacks import EarlyStopping, ReduceLROnPlateau
from keras.layers import (
    BatchNormalization,
    Conv2D,
    Dense,
    Dropout,
    GlobalAveragePooling2D,
    MaxPooling2D,
    RandomContrast,
    RandomRotation,
    RandomTranslation,
    RandomZoom,
    Rescaling,
)
from keras.models import Sequential
from sklearn.metrics import (
    accuracy_score,
    balanced_accuracy_score,
    classification_report,
    confusion_matrix,
    roc_auc_score,
)

RAIZ_REPOSITORIO = Path(__file__).resolve().parents[2]
# Ordem alfabética: é a que o image_dataset_from_directory dá às pastas de classe.
CLASSES = sorted(c.value for c in ClasseImagem)
VERSAO_VALIDA = re.compile(r'^[A-Za-z0-9][A-Za-z0-9_.-]{2,99}$')


# 4 - Construção da CNN (mesmos blocos do modelo_arroz) --------------------------------


def construir_cnn(
    tamanho: tuple[int, int],
    blocos: int = 3,
    filtros: int = 32,
    l2: float = 0.001,
    rotacao_graus: float = 20.0,
) -> keras.Model:
    """augmentation → Rescaling → blocos Conv2D/BatchNormalization/MaxPooling2D/Dropout
    → GlobalAveragePooling2D → Dense → Dropout → Dense(softmax)."""
    regularizador = keras.regularizers.L2(l2)
    branco = {'fill_mode': 'constant', 'fill_value': 255.0}  # o fundo da digital é claro

    argumentos = Sequential(
        [
            # Sem RandomFlip (ver o cabeçalho). Rotação de até ±20°, como em Michelsanti et al. 2017.
            RandomRotation(rotacao_graus / 360, **branco),
            RandomZoom(0.1, **branco),
            RandomTranslation(0.05, 0.05, **branco),
            RandomContrast(0.2),
        ],
        name='augmentation',
    )

    camadas: list = [keras.Input((*tamanho, 1)), argumentos, Rescaling(1.0 / 255)]
    for i in range(blocos):
        camadas += [
            Conv2D(filtros * 2**i, (3, 3), activation='relu', padding='same', kernel_regularizer=regularizador),
            BatchNormalization(),
            MaxPooling2D((2, 2)),
            Dropout(0.3),
        ]
    camadas += [
        GlobalAveragePooling2D(),
        Dense(64, activation='relu', kernel_regularizer=regularizador),
        Dropout(0.5),
        Dense(len(CLASSES), activation='softmax'),
    ]
    return Sequential(camadas, name='cnn_padroes')


# 6 - Treinamento: estatísticas exatas do BatchNormalization (PreciseBN) --------------
#
# O BatchNormalization guarda média e variância por média móvel exponencial
# (momentum 0,99): o valor fica dominado por ativações de estados anteriores do
# modelo. No modelo de demonstração v1 isso fez a validação oscilar entre 0,77 e
# 0,98 de uma época para outra, com o treino estável em ~0,98, e enviesou os erros
# para uma classe. Recalcular as estatísticas com os pesos fixos, sobre o treino
# sem augmentation, é o "PreciseBN" de Wu & Johnson (2021, arXiv:2105.07576).


def recalibrar_batchnorm(modelo: keras.Sequential, dados: tf.data.Dataset) -> None:
    """Troca média e variância móveis de cada BatchNormalization pelas do conjunto
    `dados` (lotes de imagens, sem augmentation), camada por camada: as estatísticas
    de uma camada são medidas depois de recalibrar as anteriores."""
    camadas = modelo.layers
    for k, normalizacao in enumerate(camadas):
        if not isinstance(normalizacao, BatchNormalization):
            continue
        soma = soma_quadrados = None
        n = 0
        for lote in dados:
            imagens = lote[0] if isinstance(lote, tuple) else lote
            ativacao = imagens
            for camada in camadas[:k]:
                ativacao = camada(ativacao, training=False)
            ativacao = tf.reshape(tf.cast(ativacao, tf.float64), (-1, ativacao.shape[-1]))
            parcial, parcial_quadrados = tf.reduce_sum(ativacao, 0), tf.reduce_sum(ativacao**2, 0)
            soma = parcial if soma is None else soma + parcial
            soma_quadrados = parcial_quadrados if soma_quadrados is None else soma_quadrados + parcial_quadrados
            n += int(ativacao.shape[0])
        if not n:
            raise ValueError('Conjunto vazio: não há como recalcular o BatchNormalization.')
        media = soma / n
        variancia = tf.maximum(soma_quadrados / n - media**2, 0.0)
        normalizacao.moving_mean.assign(tf.cast(media, normalizacao.moving_mean.dtype))
        normalizacao.moving_variance.assign(tf.cast(variancia, normalizacao.moving_variance.dtype))


class BatchNormPreciso(keras.callbacks.Callback):
    """Recalibra o BatchNormalization antes de cada validação, para que
    EarlyStopping e ReduceLROnPlateau reajam ao modelo, não ao ruído da média móvel."""

    def __init__(self, dados: tf.data.Dataset) -> None:
        super().__init__()
        self.dados = dados

    def on_test_begin(self, logs=None) -> None:
        recalibrar_batchnorm(self.model, self.dados)


# 8 - Avaliação ------------------------------------------------------------------------


def avaliar(rotulos_reais: np.ndarray, probabilidades: np.ndarray) -> dict:
    """Acurácia não basta (doc 07): sensibilidade, especificidade e AUC de cada classe."""
    previsoes = probabilidades.argmax(axis=1)
    indices = list(range(len(CLASSES)))
    matriz = confusion_matrix(rotulos_reais, previsoes, labels=indices)
    relatorio = classification_report(
        rotulos_reais, previsoes, labels=indices, target_names=CLASSES, output_dict=True, zero_division=0
    )

    por_classe = {}
    aucs = []
    for i, classe in enumerate(CLASSES):
        vp = matriz[i, i]
        fn = matriz[i].sum() - vp
        fp = matriz[:, i].sum() - vp
        vn = matriz.sum() - vp - fn - fp
        positivos = rotulos_reais == i
        auc = None
        if positivos.any() and (~positivos).any():
            auc = float(roc_auc_score(positivos, probabilidades[:, i]))
            aucs.append(auc)
        por_classe[classe] = {
            'sensibilidade': float(vp / (vp + fn)) if vp + fn else None,
            'especificidade': float(vn / (vn + fp)) if vn + fp else None,
            'precisao': relatorio[classe]['precision'],
            'f1': relatorio[classe]['f1-score'],
            'auc_um_contra_todos': auc,
            'suporte': int(matriz[i].sum()),
        }

    return {
        'imagens': len(rotulos_reais),
        'acuracia': float(accuracy_score(rotulos_reais, previsoes)),
        'acuracia_balanceada': float(balanced_accuracy_score(rotulos_reais, previsoes)),
        'auc_macro_um_contra_todos': float(np.mean(aucs)) if aucs else None,
        'por_classe': por_classe,
        'matriz_confusao': {'linhas_reais_colunas_previstas': CLASSES, 'valores': matriz.tolist()},
    }


def imprimir_matriz(metricas: dict) -> None:
    matriz = metricas['matriz_confusao']['valores']
    largura = max(len(c) for c in CLASSES) + 2
    print('\nMatriz de confusão (linhas: real; colunas: previsto):')
    print(' ' * largura + ''.join(f'{c[:8]:>10}' for c in CLASSES))
    for classe, linha in zip(CLASSES, matriz, strict=True):
        print(f'{classe:<{largura}}' + ''.join(f'{v:>10}' for v in linha))
    print('\nPor classe:')
    for classe, m in metricas['por_classe'].items():
        auc = f'{m["auc_um_contra_todos"]:.3f}' if m['auc_um_contra_todos'] is not None else '—'
        sens = f'{m["sensibilidade"]:.3f}' if m['sensibilidade'] is not None else '—'
        espec = f'{m["especificidade"]:.3f}' if m['especificidade'] is not None else '—'
        print(f'  {classe:<15} sensibilidade {sens}  especificidade {espec}  AUC {auc}  (n={m["suporte"]})')
    print(f'\nAcurácia: {metricas["acuracia"]:.4f} | Acurácia balanceada: {metricas["acuracia_balanceada"]:.4f}')


# 7 - Gráficos (SVG: o .gitignore bloqueia PNG fora da documentação) --------------------


def graficos(historico: dict, metricas: dict, pasta: Path) -> None:
    pasta.mkdir(parents=True, exist_ok=True)
    plt.figure(figsize=(12, 5))
    plt.subplot(1, 2, 1)
    plt.plot(historico['accuracy'], label='Treino')
    plt.plot(historico['val_accuracy'], label='Validação')
    plt.title('Acurácia por Época')
    plt.xlabel('Época')
    plt.ylabel('Acurácia')
    plt.legend()
    plt.subplot(1, 2, 2)
    plt.plot(historico['loss'], label='Treino')
    plt.plot(historico['val_loss'], label='Validação')
    plt.title('Perda por Época')
    plt.xlabel('Época')
    plt.ylabel('Perda')
    plt.legend()
    plt.tight_layout()
    plt.savefig(pasta / 'curvas.svg')
    plt.close()

    matriz = np.array(metricas['matriz_confusao']['valores'])
    plt.figure(figsize=(7, 6))
    plt.imshow(matriz, cmap='Blues')
    plt.xticks(range(len(CLASSES)), CLASSES, rotation=35, ha='right')
    plt.yticks(range(len(CLASSES)), CLASSES)
    for i in range(len(CLASSES)):
        for j in range(len(CLASSES)):
            cor = 'white' if matriz[i, j] > matriz.max() / 2 else 'black'
            plt.text(j, i, matriz[i, j], ha='center', va='center', color=cor)
    plt.xlabel('Previsto')
    plt.ylabel('Real')
    plt.title('Matriz de confusão (teste)')
    plt.tight_layout()
    plt.savefig(pasta / 'matriz_confusao.svg')
    plt.close()


# 9 - Previsões (figura com imagens: só na pasta local, nunca no Git) -------------------


def figura_previsoes(teste: tf.data.Dataset, modelo: keras.Model, destino: Path) -> None:
    imagens, rotulos = next(iter(teste.unbatch().batch(10)))
    previstas = modelo.predict(imagens, verbose=0).argmax(axis=1)
    _, eixos = plt.subplots(2, 5, figsize=(15, 6))
    for i, eixo in enumerate(eixos.flatten()[: len(imagens)]):
        real, previsto = CLASSES[int(np.argmax(rotulos[i]))], CLASSES[int(previstas[i])]
        eixo.imshow(imagens[i].numpy().squeeze().astype('uint8'), cmap='gray', vmin=0, vmax=255)
        eixo.set_title(f'Real: {real}\nPrevisto: {previsto}', color='green' if real == previsto else 'red', fontsize=8)
        eixo.axis('off')
    plt.suptitle('Previsões (verde=correto, vermelho=errado)', fontsize=12)
    plt.tight_layout()
    plt.savefig(destino)
    plt.close()


# 10 - Salvando o modelo ------------------------------------------------------------------


def sql_registro(versao: str, origem: str, sha256: str, metricas: dict, treinado_em: date, arquitetura: str) -> str:
    """Registro na tabela modelo_ia da API (modelo_producao/README.md, passo 4)."""
    resumo = {
        'acuracia': round(metricas['acuracia'], 4),
        'acuraciaBalanceada': round(metricas['acuracia_balanceada'], 4),
        'nTeste': metricas['imagens'],
    }
    if metricas['auc_macro_um_contra_todos'] is not None:
        resumo['auc'] = round(metricas['auc_macro_um_contra_todos'], 4)
    return (
        '-- Registra e ativa este modelo na API (um único modelo ativo por vez).\n'
        '-- Rodar no banco da aplicação depois de copiar a pasta do modelo para o serviço.\n'
        'BEGIN;\n'
        'UPDATE modelo_ia SET ativo = false WHERE ativo;\n'
        'INSERT INTO modelo_ia (id, versao, arquitetura, origem_dados, escore_validado, hash_sha256,\n'
        '                       metricas_teste, treinado_em, ativo, promovido_em)\n'
        f"VALUES (gen_random_uuid(), '{versao}', '{arquitetura}', '{origem}', false, '{sha256}',\n"
        f"        '{json.dumps(resumo)}', '{treinado_em.isoformat()}', true, now());\n"
        'COMMIT;\n'
    )


def dentro_do_repositorio(caminho: Path) -> bool:
    try:
        caminho.resolve().relative_to(RAIZ_REPOSITORIO)
        return True
    except ValueError:
        return False


def argumentos() -> argparse.Namespace:
    dados_padrao = Path(os.environ.get('TCC_DADOS_DIR', 'dados_locais')) / 'sinteticos'
    p = argparse.ArgumentParser(description='Treina a CNN de padrões (Etapa 1, ADR-0002).')
    p.add_argument('--dados', type=Path, default=dados_padrao, help='pasta com rotulos.csv (fora do repositório)')
    p.add_argument('--origem', choices=['SINTETICO', 'REAL'], required=True)
    p.add_argument('--versao', default=None, help='padrão: AAAA-MM-DD_padroes_<origem>_v1')
    p.add_argument('--tamanho', type=int, default=128, help='lado da imagem de entrada, em pixels')
    p.add_argument('--ajuste', choices=[a.value for a in AjusteProporcao], default='ESTICAR')
    p.add_argument('--blocos', type=int, default=3)
    p.add_argument('--filtros', type=int, default=32)
    p.add_argument('--lote', type=int, default=32)
    p.add_argument('--epocas', type=int, default=60)
    p.add_argument('--taxa', type=float, default=0.0003)
    p.add_argument('--paciencia', type=int, default=12)
    p.add_argument('--sem-espelho', action='store_true', help='não espelhar (com troca de alça) no treino')
    p.add_argument('--semente', type=int, default=42)
    p.add_argument('--saida-local', type=Path, default=None, help='padrão: <dados>/saidas/<versao>')
    p.add_argument('--destino-modelos', type=Path, default=RAIZ_REPOSITORIO / 'treino' / 'modelos')
    p.add_argument('--pesos-reais', type=Path, default=None, help='onde gravar o .keras REAL (fora do repositório)')
    p.add_argument('--promover-demo', action='store_true', help='copia o modelo SINTÉTICO para modelo_producao/demo')
    p.add_argument('--pasta-demo', type=Path, default=RAIZ_REPOSITORIO / 'modelo_producao' / 'demo')
    return p.parse_args()


def principal() -> Path:
    args = argumentos()
    origem = args.origem
    versao = args.versao or f'{date.today():%Y-%m-%d}_padroes_{origem.lower()}_v1'
    if not VERSAO_VALIDA.match(versao):
        raise SystemExit(f'Versão inválida: {versao!r} (use letras, números, "_", "-" e ".").')
    if origem == 'REAL':
        # ADR-0005: pesos treinados com digitais reais nunca entram no Git.
        if args.promover_demo:
            raise SystemExit('Modelo REAL não vai para modelo_producao/demo (ADR-0005).')
        if args.pesos_reais is None or dentro_do_repositorio(args.pesos_reais):
            raise SystemExit('Para origem REAL, informe --pesos-reais com uma pasta FORA do repositório.')
    if dentro_do_repositorio(args.dados):
        raise SystemExit('Os dados ficam fora do repositório (CLAUDE.md, Regra 4).')

    keras.utils.set_random_seed(args.semente)
    saida_local = args.saida_local or args.dados / 'saidas' / versao
    saida_local.mkdir(parents=True, exist_ok=True)

    # 1 - Caminhos e divisão por participante -------------------------------------------
    exemplos = divisao_mod.ler_rotulos(args.dados / 'rotulos.csv')
    desconhecidas = {e.classe for e in exemplos} - set(CLASSES)
    if desconhecidas:
        raise SystemExit(f'Classes desconhecidas em rotulos.csv: {sorted(desconhecidas)}')
    divisao = divisao_mod.dividir(exemplos, semente=args.semente)
    divisao_mod.gravar_divisao(divisao, saida_local / 'divisao.json')
    pastas = divisao_mod.montar_estrutura_virtual(divisao, saida_local / 'tmp_virtual')
    for pasta in pastas.values():  # toda classe precisa de pasta, mesmo vazia
        for classe in CLASSES:
            (pasta / classe).mkdir(parents=True, exist_ok=True)
    resumo_divisao = divisao_mod.resumo(divisao)
    print(json.dumps(resumo_divisao, indent=1, ensure_ascii=False))

    # 2 - Parâmetros -------------------------------------------------------------------
    tamanho = (args.tamanho, args.tamanho)
    carga = {
        **argumentos_dataset(tamanho, AjusteProporcao(args.ajuste)),
        'label_mode': 'categorical',
        'class_names': CLASSES,
        'batch_size': args.lote,
        'seed': args.semente,
    }

    # 3 - Carregamento dos dados -------------------------------------------------------
    treino = keras.utils.image_dataset_from_directory(pastas['treino'], shuffle=True, **carga)
    # o mesmo treino, sem augmentation nem espelho: base das estatísticas do BatchNorm
    treino_limpo = keras.utils.image_dataset_from_directory(pastas['treino'], shuffle=False, **carga)
    validacao = keras.utils.image_dataset_from_directory(pastas['validacao'], shuffle=False, **carga)
    teste = keras.utils.image_dataset_from_directory(pastas['teste'], shuffle=False, **carga)
    nome_classes = treino.class_names
    if nome_classes != CLASSES:
        raise SystemExit(f'Ordem de classes inesperada: {nome_classes}')
    print(f'Classes: {nome_classes}')

    contagem = Counter(e.classe for e in divisao['treino'])
    if not args.sem_espelho:
        treino = espelhar_aleatoriamente(treino, nome_classes, args.semente)
        # com o espelho, cada alça aparece também como a outra (as proporções valem igual)
        espelhada = Counter(
            {c: contagem[TROCA_NO_ESPELHO.get(ClasseImagem(c), ClasseImagem(c)).value] for c in CLASSES}
        )
        contagem = contagem + espelhada
    total = sum(contagem.values())
    pesos_classe = {i: total / (len(CLASSES) * contagem[c]) for i, c in enumerate(CLASSES) if contagem[c]}
    treino = treino.shuffle(64, seed=args.semente).prefetch(tf.data.AUTOTUNE)
    validacao = validacao.prefetch(tf.data.AUTOTUNE)

    # 4 - Construção da CNN --------------------------------------------------------------
    classificador = construir_cnn(tamanho, args.blocos, args.filtros)

    # 5 - Compilação ---------------------------------------------------------------------
    classificador.compile(
        optimizer=keras.optimizers.Adam(learning_rate=args.taxa),
        loss='categorical_crossentropy',
        metrics=['accuracy'],
    )
    classificador.summary()

    # 6 - Treinamento --------------------------------------------------------------------
    callback_parada = EarlyStopping(monitor='val_loss', patience=args.paciencia, restore_best_weights=True)
    callback_lr = ReduceLROnPlateau(
        monitor='val_loss', factor=0.3, patience=max(3, args.paciencia // 2), min_lr=1e-6, verbose=1
    )
    inicio = datetime.now()
    historico = classificador.fit(
        treino,
        epochs=args.epocas,
        validation_data=validacao,
        class_weight=pesos_classe,
        # BatchNormPreciso recalibra no início de cada validação (on_test_begin)
        callbacks=[BatchNormPreciso(treino_limpo), callback_parada, callback_lr],
        verbose=2,
    )
    # os pesos restaurados pelo EarlyStopping já vêm recalibrados; repetir garante o
    # mesmo resultado caso o treino termine sem restaurar
    recalibrar_batchnorm(classificador, treino_limpo)
    duracao = (datetime.now() - inicio).total_seconds()

    # 8 - Avaliação do modelo (no teste, que não foi usado em nenhuma decisão) ------------
    probabilidades, reais = [], []
    for imagens, rotulos in teste:
        probabilidades.append(classificador.predict(imagens, verbose=0))
        reais.append(rotulos.numpy().argmax(axis=1))
    metricas = avaliar(np.concatenate(reais), np.concatenate(probabilidades))
    imprimir_matriz(metricas)

    # 10 - Salvando o modelo ---------------------------------------------------------------
    pasta_versao = args.destino_modelos / ('sinteticos' if origem == 'SINTETICO' else 'reais') / versao
    if pasta_versao.exists():
        raise SystemExit(f'{pasta_versao} já existe: use outra --versao.')
    pasta_versao.mkdir(parents=True)
    arquivo_modelo = (pasta_versao if origem == 'SINTETICO' else args.pesos_reais / versao) / 'modelo.keras'
    arquivo_modelo.parent.mkdir(parents=True, exist_ok=True)
    classificador.save(arquivo_modelo)
    sha256 = sha256_arquivo(arquivo_modelo)
    (pasta_versao / 'modelo.sha256').write_text(f'{sha256}  modelo.keras\n', encoding='utf-8')

    # 7 - Gráficos (gravados aqui para irem junto com a versão) -----------------------------
    graficos(historico.history, metricas, pasta_versao / 'graficos')
    figura_previsoes(teste, classificador, saida_local / 'previsoes.png')

    arquitetura = f'CNN {args.blocos} blocos ({args.filtros}-{args.filtros * 2 ** (args.blocos - 1)} filtros)'
    config = {
        'versao': versao,
        'classes': CLASSES,
        'tamanho_imagem': list(tamanho),
        'ajuste_proporcao': args.ajuste,
        'interpolacao': 'bilinear',
        'origem_dados': origem,
        'arquitetura': arquitetura,
        'hiperparametros': {
            'blocos': args.blocos,
            'filtros_iniciais': args.filtros,
            'lote': args.lote,
            'epocas_maximas': args.epocas,
            'taxa_aprendizado': args.taxa,
            'paciencia': args.paciencia,
            'regularizacao_l2': 0.001,
            'rotacao_maxima_graus': 20,
            'espelho_aleatorio_com_troca_de_alca': not args.sem_espelho,
            'pesos_de_classe': {CLASSES[i]: round(v, 4) for i, v in pesos_classe.items()},
        },
        'semente': args.semente,
        'divisao': resumo_divisao,
        'versoes': {
            'python': platform.python_version(),
            'tensorflow': tf.__version__,
            'keras': keras.__version__,
            'numpy': np.__version__,
        },
        'treinado_em': date.today().isoformat(),
    }
    (pasta_versao / 'config.json').write_text(json.dumps(config, indent=1, ensure_ascii=False), encoding='utf-8')
    historico_resumido = {
        'epocas_treinadas': len(historico.history['loss']),
        'melhor_val_loss': float(min(historico.history['val_loss'])),
        'duracao_segundos': round(duracao),
    }
    (pasta_versao / 'metricas.json').write_text(
        json.dumps({'teste': metricas, 'treino': historico_resumido}, indent=1, ensure_ascii=False), encoding='utf-8'
    )
    (pasta_versao / 'registro.sql').write_text(
        sql_registro(versao, origem, sha256, metricas, date.today(), arquitetura), encoding='utf-8'
    )
    (pasta_versao / 'NOTAS.md').write_text(
        f'# {versao}\n\n'
        f'- Origem dos dados: **{origem}**'
        + (
            ' (gerador procedural `treino/scripts/sinteticos.py`; não deve ser interpretado).\n'
            if origem == 'SINTETICO'
            else '.\n'
        )
        + f'- {resumo_divisao["teste"]["imagens"]} imagens de teste, '
        f'de {resumo_divisao["teste"]["participantes"]} participantes.\n'
        f'- Acurácia balanceada no teste: {metricas["acuracia_balanceada"]:.4f}.\n'
        '- O que mudou em relação à versão anterior: (preencher).\n',
        encoding='utf-8',
    )

    if args.promover_demo:
        args.pasta_demo.mkdir(parents=True, exist_ok=True)
        for nome in ('modelo.keras', 'modelo.sha256', 'config.json'):
            shutil.copy2(pasta_versao / nome, args.pasta_demo / nome)
        print(f'Modelo copiado para {args.pasta_demo} (demonstração).')

    print(f'\nVersão {versao} gravada em {pasta_versao}')
    if origem == 'REAL':
        print(f'Pesos REAIS em {arquivo_modelo}: guarde em local controlado, nunca no Git (ADR-0005).')
    return pasta_versao


if __name__ == '__main__':
    principal()
