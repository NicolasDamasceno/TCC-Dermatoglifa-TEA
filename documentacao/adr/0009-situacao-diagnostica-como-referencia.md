# ADR-0009: Situação diagnóstica como padrão de referência

**Status:** Proposto
**Data:** 2026-11-05
**Decisores:** Nicolas Damasceno (autor), orientador do TCC

## Contexto

A UC13 permite ao médico registrar o resultado do diagnóstico clínico de TEA,
fechado fora do sistema. O doc 03 justifica a UC13 assim: "sem esse dado não é
possível medir, no futuro, se os indicadores têm relação com o diagnóstico real".
Isso faz da situação diagnóstica o **padrão de referência** de qualquer avaliação
do sistema, inclusive do treino da Etapa 2 (ADR-0002).

O QUADAS-2 é a ferramenta usual para avaliar estudos de acurácia diagnóstica
(Whiting et al., 2011, *Annals of Internal Medicine* 155:529–536). Dois de seus
domínios afetam diretamente o que o sistema precisa guardar (perguntas em
tradução livre):

- **Domínio 3, padrão de referência:** "os resultados do padrão de referência
  foram interpretados sem conhecimento dos resultados do teste índice?". Se o
  resultado da análise influenciar o diagnóstico, a avaliação do sistema fica
  enviesada a seu favor.
- **Domínio 4, fluxo e tempo:** "houve intervalo adequado entre o(s) teste(s)
  índice e o padrão de referência?". Sem a data da avaliação clínica, esse
  intervalo não pode ser calculado.

Até aqui o modelo de dados guardava só `situacao_diagnostica`,
`fonte_diagnostico` e `situacao_atualizada_em`. Esta última é a data do registro
no sistema, não a da avaliação clínica.

## Decisão

1. **Nova coluna `paciente.diagnostico_avaliado_em` (date).** Guarda o dia da
   avaliação clínica que definiu a situação. É obrigatória fora de `SUSPEITA`, não
   pode estar no futuro nem ser anterior ao nascimento. Uma restrição no banco
   (`ck_paciente_diagnostico_completo`) garante que fonte e data existam juntas
   fora da suspeita e estejam ambas vazias nela.
2. **Declaração de independência obrigatória.** Para registrar `TEA_CONFIRMADO`,
   `TEA_DESCARTADO` ou `CONTROLE`, o médico declara que "o diagnóstico foi fechado
   sem considerar o resultado deste sistema". Sem a declaração, a API responde
   `422`. A declaração não é gravada como coluna, porque seria sempre verdadeira.
   O registro na auditoria mostra que a atualização passou por ela.
3. **Situações aceitas:**
   - `TEA_CONFIRMADO` e `TEA_DESCARTADO`, como no doc 03;
   - `CONTROLE`, para participantes do grupo controle (doc 05), já que não havia
     outra forma de registrá-la;
   - a volta para `SUSPEITA`, só para corrigir um registro feito por engano. Ela
     apaga fonte e data.

   Uma atualização que não muda nada é recusada.
4. **Auditoria.** A ação `SITUACAO_DIAGNOSTICA_ATUALIZADA` guarda só a transição
   (`situacao_anterior`, `situacao_nova`). A fonte é texto livre e fica fora do
   log, pela mesma regra que impede nomes nos detalhes (RNF07). O histórico de
   transições fica, portanto, na auditoria.
5. **Rota:** `PATCH /api/pacientes/{id}/situacao-diagnostica`, só para o médico
   responsável (RN05). Médico de outro paciente recebe `404` e o administrador
   recebe `403`.

## Opções consideradas

| Opção | Prós | Contras |
|---|---|---|
| Só situação e fonte (o modelo anterior) | Nenhuma mudança no banco | Não mede o intervalo nem registra a independência (QUADAS-2, domínios 3 e 4) |
| **Data da avaliação + declaração obrigatória (escolhida)** | Dá o que o QUADAS-2 pergunta; uma coluna e uma restrição | O médico precisa saber a data da avaliação |
| Registrar também os diagnósticos influenciados pela IA, marcados à parte | Não perde casos | Mistura referências enviesadas e independentes na mesma base; exige separar na análise |
| Tabela de histórico de diagnósticos | Histórico completo, com fonte | Mais uma tabela; a auditoria já guarda as transições |

## Consequências

- A avaliação futura pode excluir casos sem referência independente e calcular o
  intervalo entre coleta e diagnóstico.
- Diagnósticos influenciados pela análise não entram no sistema. Se o protocolo
  precisar contá-los (por exemplo, para relatar perdas), isso vira uma decisão
  nova.
- A declaração depende da honestidade de quem registra. O sistema não tem como
  verificar se o diagnóstico foi de fato independente.
- **Revisitar** quando o protocolo do CEP definir o instrumento diagnóstico de
  referência: a fonte, hoje texto livre, pode virar uma lista fechada.

## Ações

1. [x] Domínio, migração, rota, auditoria e testes (back).
2. [x] Seção "Situação diagnóstica" na página do paciente (front).
3. [ ] Definir no protocolo do CEP o padrão de referência (instrumento e equipe) e
   o intervalo máximo aceito entre coleta e diagnóstico.
