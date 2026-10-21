# ADR-0001: Inferência em um serviço Python separado, chamado pela API .NET

**Status:** Proposto
**Data:** 2026-10-20
**Decisores:** Nicolas Damasceno (autor), orientador do TCC

## Contexto

O modelo é treinado em Python/Keras (aulas do 3º período; `treino/`). O site usa
back-end .NET e front-end React. Depois que o médico coleta as 10 digitais, o
sistema precisa executar o modelo de `modelo_producao/` e devolver o resultado.

Forças em jogo:
- o **pré-processamento** da inferência precisa ser idêntico ao do treino
  (tamanho, escala de cinza, normalização); divergência entre os dois é uma
  causa clássica de queda silenciosa de desempenho;
- o autor domina Python para IA e está aprendendo .NET;
- o volume é baixo (uma análise por consulta), latência de alguns segundos é aceitável;
- as imagens são dados sensíveis: quanto menos componentes as recebem, melhor.

## Decisão

Criar um **Serviço de IA em Python** (FastAPI), acessível apenas pela rede
interna, que carrega o modelo de `modelo_producao/` na inicialização e expõe
`POST /inferencia` e `GET /saude`. A API .NET é o único cliente. O serviço não
acessa banco e não grava imagens em disco.

## Opções consideradas

### Opção A: serviço Python (FastAPI) separado
| Dimensão | Avaliação |
|---|---|
| Complexidade | Média (dois processos) |
| Custo | Nenhum |
| Fidelidade ao treino | Alta — reutiliza o mesmo código de pré-processamento |
| Familiaridade | Alta (Python) |

**Prós:** mesmo código e mesma biblioteca do treino; trocar o modelo é trocar
o arquivo; fácil de testar isoladamente com imagens sintéticas.
**Contras:** mais um processo para subir; contrato HTTP a manter.

### Opção B: exportar para ONNX e executar com ONNX Runtime dentro do .NET
| Dimensão | Avaliação |
|---|---|
| Complexidade | Média (conversão + reimplementar pré-processamento em C#) |
| Fidelidade ao treino | Média — pré-processamento duplicado em duas linguagens |
| Familiaridade | Baixa |

**Prós:** um processo só; sem Python no servidor.
**Contras:** conversão Keras→ONNX pode falhar em camadas específicas; o
pré-processamento passa a existir em Python (treino) e em C# (produção).

### Opção C: a API .NET executa um script Python por linha de comando
**Prós:** sem servidor HTTP extra.
**Contras:** carrega o modelo a cada análise (lento); passagem de imagens por
arquivos temporários (risco de sigilo); tratamento de erro frágil.

## Análise de trade-offs

A opção A troca um pouco de complexidade operacional pela garantia de que treino
e produção usam exatamente o mesmo pré-processamento — o risco mais caro num
projeto de IA. B só compensa se for preciso eliminar Python do servidor. C é
descartada por desempenho e por gravar imagens temporárias.

## Consequências

- Mais fácil: evoluir o modelo sem mexer no .NET; reaproveitar código do treino.
- Mais difícil: subir dois serviços (resolvido com `docker compose` ou scripts).
- Revisitar: se a entrega exigir um único executável, reavaliar a opção B.

## Contrato (rascunho)

`POST /inferencia` — `multipart/form-data`
- 10 arquivos nomeados `<MAO>_<DEDO>` (ex.: `DIREITA_POLEGAR`);
- campos `sexo` (`MASCULINO|FEMININO`) e `idade_anos` (inteiro).

Resposta `200`:
```json
{
  "versao_modelo": "2026-11-15_padroes_v1",
  "digitais": [
    { "mao": "DIREITA", "dedo": "POLEGAR",
      "classe_imagem": "VERTICILO", "padrao": "VERTICILO",
      "confianca": 0.91,
      "probabilidades": { "ARCO": 0.01, "ARCO_TENDIDO": 0.02,
                          "ALCA_ESQUERDA": 0.05, "ALCA_DIREITA": 0.01, "VERTICILO": 0.91 },
      "alerta_qualidade": false }
  ],
  "indicadores": { "qtd_verticilos": 4, "qtd_alcas_ulnares": 5, "qtd_alcas_radiais": 0,
                   "qtd_arcos": 1, "qtd_arcos_tendidos": 0, "discordancia_bilateral": 2,
                   "discordancia_bilateral_com_polegar": 2 },
  "escore_risco": null
}
```

`escore_risco` é `null` enquanto não houver modelo de risco validado (ver ADR-0002).
Nenhum identificador do paciente trafega nessa chamada.

## Ações

1. [ ] Criar o serviço FastAPI em `modelo_producao/servico/`, junto do modelo que
   ele carrega (mantém as quatro pastas definidas para o repositório).
2. [ ] Compartilhar o módulo de pré-processamento entre treino e serviço.
3. [ ] Implementar `ClienteInferenciaHttp` no back-end com timeout e tratamento de erro.
