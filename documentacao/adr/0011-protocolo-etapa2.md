# ADR-0011: Protocolo de treino e avaliação da Etapa 2 (escore)

**Status:** Proposto
**Data:** 2026-11-08
**Decisores:** Nicolas Damasceno (autor), orientador do TCC

## Contexto

A ADR-0002 decidiu que o escore vem de uma regressão logística sobre os
indicadores, o sexo e a idade, treinada só quando houver casos e controles
suficientes. Até lá, `escore_risco` é `null` (RN10). Ela deixou abertas as
escolhas que decidem se o número final quer dizer alguma coisa:

1. **Quais variáveis entram.** Dos achados de padrão em TEA, só a discordância
   bilateral se replicou, em duas amostras só de meninos. As frequências de
   padrão têm direções opostas entre estudos (doc 07). Testar várias combinações
   e ficar com a melhor infla o desempenho.
2. **Confusão pela amostragem.** Num caso-controle, idade e sexo podem diferir
   entre os grupos por causa do recrutamento. Em Kyselicová et al. (2023), os
   grupos não estavam pareados por idade (5,1 vs 8,6 anos; doc 07). Um modelo
   com idade pode "acertar" o grupo sem usar a dermatoglifia **[INFERÊNCIA]**.
3. **Amostra pequena.** A regra de 10 eventos por variável é simplista. Riley et
   al. (2020) propõem critérios que dependem do número de parâmetros
   *candidatos*, da proporção de casos e do desempenho esperado: precisão da
   proporção, erro de previsão, encolhimento de 0,9 e otimismo do R² de 0,05.
   O pacote pmsampsize faz o cálculo.
4. **De onde vêm os padrões.** No sistema, quem informa o padrão é a CNN, com
   erros. Se a Etapa 2 for avaliada com padrões da CNN em participantes que a CNN
   viu no treino, a avaliação fica otimista (ADR-0002, protocolo mínimo).
5. **Prevalência.** Num caso-controle, a proporção de casos é escolhida pelo
   desenho. O intercepto e as probabilidades refletem essa proporção, não a da
   população; VPP e VPN dependem da prevalência (doc 07).

## Decisão

Script `treino/scripts/etapa2.py`, com dados sempre fora do repositório:

- **Variáveis pré-especificadas.** O conjunto padrão `minimo` tem a discordância
  bilateral sem polegar (0–4, de Bruin et al. 2014), o sexo e a idade em meses.
  O conjunto `contagens` acrescenta arcos, arcos tendidos, alças radiais e
  verticilos; a alça ulnar fica como referência porque as cinco somam 10. A
  escolha é feita **antes** de ver os dados e registrada no `config.json`. Uma
  variável constante no treino sai e fica registrada (com só meninos, o sexo sai).
- **Indicadores pelo mesmo código do serviço.** `calcular_indicadores` de
  `dermatoglifia_ia`, o mesmo que a API confere. Participantes sem os 10 dedos
  saem e são contados (RN09).
- **Divisão por participante.** O teste é separado primeiro, com a proporção de
  TEA da amostra, e usado uma única vez. Com `--origem-padroes ETAPA1`, é
  obrigatório informar a `divisao.json` da CNN, e o teste só recebe quem não
  esteve no treino nem na validação da Etapa 1.
- **Modelo.** Regressão logística com penalidade L2 sobre as variáveis
  padronizadas. O encolhimento reduz o sobreajuste em amostra pequena (Riley et
  al. 2020). O C sai de uma grade fixa pela menor log-loss em validação cruzada
  estratificada **só no treino**; no empate, fica o C menor.
- **Ponto de corte.** Índice de Youden nas previsões fora da dobra do treino. É
  só o corte usado para calcular sensibilidade e especificidade: o sistema
  continua sem ponto de corte validado (alerta `SEM_PONTO_DE_CORTE_VALIDADO`).
- **Métricas no teste** (ADR-0002; TRIPOD+AI):
  - sensibilidade e especificidade com IC 95% de Wilson, um método de escore
    (Newcombe 1998);
  - ROC-AUC e PR-AUC com IC 95% por bootstrap estratificado;
  - VPP e VPN nas prevalências declaradas: por padrão 1/31 (ADDM 2022, doc 07) e
    0,30 (cenário encaminhado do doc 07). Não há estimativa brasileira;
  - calibração por intercepto (alvo 0), inclinação (alvo 1) e Brier. Com menos
    de 200 casos e 200 controles, Van Calster et al. (2019) consideram defensável
    avaliar só intercepto e inclinação; o gráfico por faixas é só uma leitura
    grosseira.
- **Comparação com um modelo só de sexo e idade**, treinado do mesmo jeito. O
  resultado que interessa é o **ganho de AUC** dos indicadores sobre ele, com IC
  por bootstrap pareado. A idade média por grupo no treino também é gravada.
- **Tamanho da amostra.** O script grava eventos por parâmetro candidato e a
  margem de erro da proporção de casos (critério 1 de Riley et al. 2020), e avisa
  quando há menos de 10 por parâmetro. O cálculo completo é feito com o
  pmsampsize, antes da coleta.
- **Saída.** Em `treino/modelos/<sinteticos|reais>/<versao>/`:
  - `coeficientes.json` traz os coeficientes nas escalas padronizada e original,
    as razões de chances por unidade, o ponto de corte, a fração de casos do
    treino, a fórmula de correção do intercepto para uma prevalência τ (King &
    Zeng 2001) e `validado: false`;
  - `config.json`, `metricas.json` (só agregados), `NOTAS.md` e gráficos em SVG.
  - Os coeficientes podem ser públicos (ADR-0005). Códigos de participante,
    divisão e previsões ficam só na pasta local.
- **Padrões da CNN.** `treino/scripts/prever_padroes.py` aplica um modelo da
  Etapa 1 às imagens com o carregamento, o pré-processamento e a conversão de
  alças do serviço, e grava o `padroes.csv`.
- **Dados sintéticos.** `treino/scripts/sinteticos_etapa2.py` gera participantes
  com grupo **inventado** e efeito configurável na discordância. Os valores
  padrão reproduzem as médias de de Bruin et al. (2014), 1,80 e 2,24 pares
  discordantes. Servem para testar o código e para ver o tamanho do sinal que o
  protocolo consegue medir.

### O que a simulação mostra

Com 300 participantes sintéticos e as médias de de Bruin, a discordância vira
Binomial(4; 0,45) contra Binomial(4; 0,56) e a AUC teórica é 0,62
**[INFERÊNCIA: cálculo próprio]**. O script estimou 0,56 no teste de 75
participantes (IC 95% 0,43–0,70). Mesmo com o efeito publicado e uma amostra
maior que a esperada no TCC, o intervalo pode incluir 0,5. Isso é coerente com o
doc 07, que estimou AUC ≈ 0,65 a partir de d = 0,55. É um argumento para
apresentar o escore como hipótese a testar, não como ferramenta.

### O que esta ADR não decide

- **Quando o escore passa a aparecer.** `validado` só muda para `true` com uma
  ADR nova que diga qual critério foi atingido, combinado com o orientador antes
  de ver o teste. Até lá, valem a RN10 e a trava do domínio, que descarta o
  escore de um modelo com `escore_validado = false`.
- **A integração com o serviço e a API.** O serviço ainda devolve `null`. Para
  usar a Etapa 2, cada análise precisará registrar também a versão da Etapa 2
  (RN06), o que muda o modelo de dados (doc 05) e o contrato (ADR-0001).

## Opções consideradas

| Opção | Prós | Contras |
|---|---|---|
| **Logística L2 com variáveis pré-especificadas (escolhida)** | Interpretável (razões de chances), poucos parâmetros, encolhimento contra sobreajuste | Coeficientes encolhidos; razões de chances menores que as de um ajuste sem penalidade |
| Logística sem penalidade, com seleção passo a passo | Saída igual à de artigos clássicos (p-valores) | Selecionar pelo resultado infla o desempenho; instável em amostra pequena |
| Floresta aleatória ou *boosting* | Captura interações | Menos interpretável; pede muito mais dados; foge da ADR-0002 |
| Usar só a discordância, sem modelo | O mais simples | Não ajusta por sexo e idade; não gera probabilidade |

## Consequências

- Fica possível treinar e avaliar a Etapa 2 assim que houver dados reais, com o
  mesmo protocolo para anotação manual e para padrões da CNN.
- A métrica principal deixa de ser a AUC sozinha e passa a ser o ganho sobre
  sexo e idade, o que protege contra a confusão do desenho caso-controle.
- Com as amostras típicas de TCC, os intervalos serão largos. O texto do TCC
  deve dizer isso em vez de reportar só o valor pontual.
- **Revisitar** se o protocolo do CEP parear os grupos por idade e sexo (o
  modelo de covariáveis perde sentido) ou se a contagem de cristas entrar
  (ADR-0002).

## Ações

1. [x] Treino e avaliação da Etapa 2 com testes, só com dados sintéticos
   (`treino/scripts/etapa2.py`, `sinteticos_etapa2.py`, `prever_padroes.py`).
2. [ ] Calcular o tamanho mínimo da amostra com o pmsampsize (desempenho esperado
   tirado do doc 07) e levar ao protocolo do CEP.
3. [ ] Combinar com o orientador, antes de ver o teste real, o critério que torna
   o escore `validado`.
4. [ ] Integrar a Etapa 2 ao serviço e à API, com a versão da Etapa 2 em cada
   análise (ADR nova).

## Referências

- Riley RD, Ensor J, Snell KIE, et al. Calculating the sample size required for
  developing a clinical prediction model. *BMJ* 2020;368:m441.
  doi:10.1136/bmj.m441.
- Van Calster B, McLernon DJ, van Smeden M, Wynants L, Steyerberg EW. Calibration:
  the Achilles heel of predictive analytics. *BMC Med* 2019;17:230.
  doi:10.1186/s12916-019-1466-7.
- Newcombe RG. Two-sided confidence intervals for the single proportion:
  comparison of seven methods. *Stat Med* 1998;17(8):857–872. PMID 9595616.
- King G, Zeng L. Logistic regression in rare events data. *Political Analysis*
  2001;9(2):137–163.
- Pacote pmsampsize (CRAN): cálculo de Riley et al. (2020).
- Demais fontes (de Bruin 2014, Kyselicová 2023, TRIPOD+AI, ADDM 2022): doc 07.
