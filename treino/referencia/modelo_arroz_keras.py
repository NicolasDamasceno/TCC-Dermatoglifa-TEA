# -*- coding: utf-8 -*-
"""
REFERÊNCIA — modelo das aulas de IA do 3º período (classificação de grãos de arroz).

Este arquivo é mantido sem alterações como base de estrutura para o modelo de
digitais. Ele NÃO é usado pelo sistema. As adaptações obrigatórias para o
domínio de digitais estão descritas no CLAUDE.md (Regra 3) e no ADR-0002.

Original gerado no Colab (Untitled6.ipynb).
"""

from google.colab import drive
drive.mount('/content/drive', force_remount=True)

import os
import shutil
import numpy as np
import matplotlib.pyplot as plt
import keras
from keras.models import Sequential
from keras.layers import (Conv2D, MaxPooling2D, GlobalAveragePooling2D,
                          Dense, Dropout, BatchNormalization, Rescaling,
                          RandomFlip, RandomRotation, RandomZoom, RandomContrast)
from keras.callbacks import EarlyStopping, ReduceLROnPlateau
from sklearn.metrics import confusion_matrix, accuracy_score, classification_report

# 1 - Montagem dos caminhos do dataset
BASE_DIR = "/content/drive/MyDrive/Inteligência Artificial/Projeto_Final/dataset_arroz"
CLASSES = ['graos_inteiros', 'grao_quebrado']

# 1.1 - Montagem do dataset de treino para arroz inteiro e quebrado
treino_inteiro = keras.utils.image_dataset_from_directory(
    os.path.join(BASE_DIR, 'graos_inteiros', 'train'),
    image_size=(64,64),
    batch_size=None, # Sem batch por agora, fase de montagem
    label_mode=None, # Sem rotulos ainda
    shuffle=True,
    seed=42
)

treino_quebrado = keras.utils.image_dataset_from_directory(
    os.path.join(BASE_DIR, 'grao_quebrado', 'train'),
    image_size=(64, 64),
    batch_size=None,
    label_mode=None,
    shuffle=True,
    seed=42
)

import shutil

def montar_estrutura_virtual(base_dir, split, dest_root):
    # Cria uma estrutra de split /classe pelo o Keras
    dest = os.path.join(dest_root, split)
    if os.path.exists(dest):
        return dest

    os.makedirs(dest)

    for cls in CLASSES:
        src = os.path.abspath(os.path.join(base_dir, cls, split))
        lnk = os.path.join(dest, cls)

        if os.path.exists(src):
            os.symlink(src, lnk)

    return dest

VIRTUAL_ROOT = '/content/tmp_virtual'
TREINO_VIRTUAL = montar_estrutura_virtual(BASE_DIR, 'train', VIRTUAL_ROOT)
TESTE_VIRTUAL  = montar_estrutura_virtual(BASE_DIR, 'test',  VIRTUAL_ROOT)

# 2 - Parâmentros
IMG_SIZE = (64,64)
BARTCH_SIZE = 8
EPOCHS = 100
NUM_CLASSES = len(CLASSES)

# 3 - Carregamento dos dados
treino = keras.utils.image_dataset_from_directory(
    TREINO_VIRTUAL,
    image_size=IMG_SIZE,
    batch_size=BARTCH_SIZE,
    label_mode='categorical',
    validation_split=0.20,
    subset='training',
    seed=42
)

validacao = keras.utils.image_dataset_from_directory(
    TREINO_VIRTUAL,
    image_size=IMG_SIZE,
    batch_size=BARTCH_SIZE,
    label_mode='categorical',
    validation_split=0.20,
    subset='validation',
    seed=42
)

teste = keras.utils.image_dataset_from_directory(
    TESTE_VIRTUAL,
    image_size=IMG_SIZE,
    batch_size=BARTCH_SIZE,
    label_mode='categorical',
    shuffle=False # Sem embaralhar para alinhar as previsões com rótulos
)

nome_classes = treino.class_names
print(f'Classes: {nome_classes}')

# 4 - Construindo da CNN

regularizador = keras.regularizers.L2(0.001)

argumentos = Sequential([
    RandomFlip('horizontal_and_vertical'),
    RandomRotation(0.15),
    RandomZoom(0.15),
], name='augmentation')

classificador = Sequential([
    # Normalização e argumentos
    argumentos,
    Rescaling(1./255),

    # Primeiro bloco
    Conv2D(32, (3, 3), activation='relu', kernel_regularizer=regularizador),
    BatchNormalization(),
    MaxPooling2D((2,2)),
    Dropout(0.3),

    # Segundo Bloco
    Conv2D(64, (3, 3), activation='relu', kernel_regularizer=regularizador),
    BatchNormalization(),
    MaxPooling2D((2, 2)),
    Dropout(0.3),

    # Camadas densas
    GlobalAveragePooling2D(),
    Dense(32, activation='relu',kernel_regularizer=regularizador),
    Dropout(0.5),

    Dense(NUM_CLASSES, activation='sigmoid')
])


# 5 - Compilação
classificador.compile(
    optimizer=keras.optimizers.Adam(learning_rate=0.0003),
    loss='categorical_crossentropy',
    metrics=['accuracy']
)

classificador.summary()

# 6 - CNN treinamento
callback_parada = EarlyStopping(
    monitor='val_loss',
    patience=30,
    restore_best_weights=True
)

callback_lr = ReduceLROnPlateau(
    monitor='val_loss',
    factor=0.3,
    patience=8,
    min_lr=1e-6,
    verbose=1
)

historico = classificador.fit(
    treino,
    epochs=EPOCHS,
    validation_data=validacao,
    callbacks=[callback_parada, callback_lr],
    verbose=1
)


# 7 - Construindo os Gráficos
plt.figure(figsize=(12,5))

plt.subplot(1, 2, 1)
plt.plot(historico.history['accuracy'], label='Treino')
plt.plot(historico.history['val_accuracy'], label='Validação')
plt.title('Acurácia por Época')
plt.xlabel('Época')
plt.ylabel('Acurácia')
plt.legend()

plt.subplot(1, 2, 2)
plt.plot(historico.history['loss'], label= 'Treino')
plt.plot(historico.history['val_loss'], label='Validação')
plt.title('Perda por Época')
plt.xlabel('Época')
plt.ylabel('Perda')
plt.legend()

plt.tight_layout()
plt.show()

# 8 - Avaliação do Modelo
probs_lista = []
rotulos_lista = []

for imgs, rots in teste:
    probs_lista.append(classificador.predict(imgs, verbose=0))
    rotulos_lista.append(rots.numpy())

probabilidade = np.concatenate(probs_lista, axis=0)
rotulos_reais = np.concatenate(rotulos_lista, axis=0)
previsoes = np.argmax(probabilidade, axis=1)
rotulos_reais_idx = np.argmax(rotulos_reais, axis=1)

precisao = accuracy_score(rotulos_reais_idx, previsoes)
matriz = confusion_matrix(rotulos_reais_idx, previsoes)

print(f'\n{'='*40}')
print(f'Acurácia do Teste: {precisao:.4f} ({precisao*100:.2f}%)')
print('\nMatriz de Confusão: ')
print('\nRelatório de Classificação:')
print(classification_report(rotulos_reais_idx, previsoes, target_names=nome_classes))
resultado = classificador.evaluate(teste, verbose=1)
print(f'\nPerda: {resultado[0]:.4f} | Acurácia: {resultado[1]:.4f}')

# 9 - Previsões
todas_imagens = []
todos_rotulos = []

for imgs, rots in teste:
    todas_imagens.append(imgs.numpy())
    todos_rotulos.append(rots.numpy())
    if sum(len(x) for x in todas_imagens) >= 10:
        break

todas_imagens = np.concatenate(todas_imagens, axis=0)[:10]
todos_rotulos = np.concatenate(todos_rotulos, axis=0)[:10]

fig, axes = plt.subplots(2, 5, figsize=(15, 6))
axes = axes.flatten()

for i in range(len(todas_imagens)):
    ax = axes[i]
    ax.imshow(todas_imagens[i].astype('uint8'))
    real     = nome_classes[np.argmax(todos_rotulos[i])]
    img_tensor = np.expand_dims(todas_imagens[i], axis=0)
    previsto = nome_classes[np.argmax(classificador.predict(img_tensor, verbose=0))]
    cor = 'green' if real == previsto else 'red'
    ax.set_title(f"Real: {real}\nPrevisto: {previsto}", color=cor, fontsize=8)
    ax.axis('off')

plt.suptitle('Previsões (verde=correto, vermelho=errado)', fontsize=12)
plt.tight_layout()
plt.show()

# 10 - Salvando o modelo
SAVE_DIR = '/content/drive/MyDrive/Inteligência Artificial/Projeto_Final'
classificador.save(os.path.join(SAVE_DIR, 'modelo_arroz_cnn.keras'))
print('Modelo salvo!')

shutil.rmtree(VIRTUAL_ROOT, ignore_errors=True)
