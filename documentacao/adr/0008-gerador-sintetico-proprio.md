# ADR-0008: Gerador próprio de digitais sintéticas para o modelo de demonstração

**Status:** Proposto
**Data:** 2026-11-04
**Decisores:** Nicolas Damasceno (autor), orientador do TCC

## Contexto

O ADR-0005 decidiu publicar uma versão da Etapa 1 treinada **só com dados
sintéticos**, para que qualquer clone do repositório rode o sistema inteiro. Para
isso é preciso um conjunto de digitais com rótulo de padrão (as 5 classes do
ADR-0002) que possa ser gerado e redistribuído sem dúvida jurídica. As opções
levantadas no doc 07 e no ADR-0005 tinham problemas:

- **NIST SD4/SD14**: retirados pelo NIST; cópias de terceiros têm direitos de
  redistribuição incertos (doc 07).
- **SOCOFing**: sem rótulo de padrão, só para pesquisa não comercial (doc 07).
- **Roboflow**: origem não declarada, sem arco tendido nem direção da alça (doc 07).
- **Anguli** e **SFinGe**: candidatos do ADR-0005, com licença e resolução ainda
  não verificadas. Treinar o modelo público com eles exigiria antes essa
  verificação, e o resultado (pesos derivados) seria publicado.

Nenhuma coleta real pode acontecer antes da aprovação do CEP (doc 07; CLAUDE.md
Regra 4). O modelo de demonstração precisa existir antes disso.

## Decisão

Escrever um **gerador procedural próprio** (`treino/scripts/sinteticos.py`), sem
dependência de base ou software de terceiros, a partir de dois métodos publicados:

1. **Campo de orientação** das cristas pelo modelo zero-polo de Sherlock & Monro
   (*Pattern Recognition* 26(7):1047–1055, 1993,
   [doi:10.1016/0031-3203(93)90006-I](https://doi.org/10.1016/0031-3203(93)90006-I)):
   núcleos e deltas posicionados no plano definem as alças, o verticilo e o arco
   tendido. O arco simples, que não tem singularidades, usa curvas de nível de uma
   "lombada" suave.
2. **Cristas** crescidas a partir de ruído por filtragem de Gabor iterada, alinhada
   ao campo, como no SFinGe (Cappelli et al., ICPR 2000,
   [doi:10.1109/ICPR.2000.903586](https://doi.org/10.1109/ICPR.2000.903586)). Não
   se usa código do SFinGe, só a ideia publicada.

A convenção de lado é a da tabela de conversão do back (ADR-0002): ALCA_DIREITA é a
alça cujas cristas entram e saem pela direita da imagem, com o delta à esquerda. Um
teste confere essa orientação no campo gerado.

Cada "participante" sintético tem os 10 dedos, com a classe de cada dedo sorteada
de modo uniforme, para que a divisão por participante (Regra 3) seja exercitada
exatamente como será com dados reais.

## Opções consideradas

| Opção | Prós | Contras |
|---|---|---|
| **Gerador próprio (escolhida)** | Licença limpa; reprodutível por semente; convenção de lado sob controle; roda em qualquer máquina | Imagens simplificadas: sem poros, cicatrizes, dedos parciais, pressão irregular nem a variedade real de formas |
| Anguli | Gera as classes de Henry com distribuição configurável | Licença e redistribuição dos pesos a verificar; ferramenta externa |
| SFinGe (versão de demonstração) | Imagens mais realistas; usado por Peralta et al. | Software proprietário; licença a verificar |
| Nenhum modelo público | Nenhum trabalho extra | Clones do repositório não rodam o sistema (contraria o ADR-0005) |

## Consequências

- **As métricas do modelo de demonstração não dizem nada sobre digitais reais.**
  As imagens sintéticas são mais regulares que as reais, então a acurácia no teste
  sintético tende a ser otimista; o que vale para o TCC é a avaliação com a coleta
  aprovada pelo CEP. A API já marca todo resultado desse modelo com o alerta
  `MODELO_SINTETICO` ("não deve ser interpretado").
- O modelo sintético pode servir de ponto de partida (pré-treino) para a versão
  real, mas o ganho é incerto por causa da diferença de domínio (doc 07); isso
  precisa ser medido, comparando com o treino do zero.
- O gerador é testado como o resto do código (formato, 500 ppi, 10 dedos por
  participante, mesma semente gera as mesmas imagens, orientação da alça).
- **Revisitar** se o Anguli ou o SFinGe forem liberados (licença verificada): eles
  podem gerar uma versão sintética mais realista para pré-treino.

## Ações

1. [x] Gerador, divisão por participante e treino da Etapa 1 com testes (`treino/`).
2. [ ] Primeiro modelo de demonstração em `modelo_producao/demo/` (treino em andamento; validação ainda oscila entre épocas, a investigar).
3. [ ] Comparar, quando houver dados reais, pré-treino sintético contra treino do zero.
