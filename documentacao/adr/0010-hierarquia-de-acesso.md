# ADR-0010: Hierarquia de acesso: o administrador vê os dados dos pacientes

**Status:** Proposto
**Data:** 2026-11-05
**Decisores:** Nicolas Damasceno (autor), orientador do TCC

## Contexto

Até aqui, o administrador não via dados de pacientes. Só os médicos tinham acesso,
cada um aos próprios pacientes (RN05), e o administrador recebia `403`. O doc 03,
porém, dá ao administrador a UC12: atender os pedidos do titular pela LGPD
(art. 18), que são acesso, portabilidade, correção e eliminação. Exportar ou
corrigir os dados de alguém exige vê-los, então as duas regras se contradiziam.

Em 2026-11-05, Nicolas decidiu que o administrador fica acima do médico numa
hierarquia de acesso: vê tudo e atende os pedidos do titular, mas não faz as ações
clínicas.

## Decisão

| Ação | Médico responsável | Outro médico | Administrador |
|---|---|---|---|
| Listar e abrir pacientes, coletas e resultados | sim (os dele) | não (`404`) | sim (todos) |
| Cadastrar paciente, registrar termo, coletar, registrar situação diagnóstica, observação, prontuário, nova tentativa | sim | não | **não** (`403`) |
| Revogar termo a pedido do titular | sim | não | sim |
| Exportar, corrigir identificação, eliminar (UC12) | não (`403`) | não | sim |

- **Ações clínicas continuam só com o médico responsável.** Coletar, registrar o
  diagnóstico e confirmar o uso da IA no prontuário são atos do médico (Res. CFM
  2.454/2026; ADR-0009).
- **Toda leitura do administrador fica na auditoria**, com o identificador dele:
  abrir o paciente (`PACIENTE_VISUALIZADO`), a lista e os resultados. A trilha
  (UC11) mostra quem viu o quê.
- **UC12:**
  - *Exportar* gera um .zip com `dados.json` (tudo o que o sistema guarda do
    paciente) e as imagens decifradas.
  - *Corrigir* altera nome, nascimento e responsável, com as mesmas regras do
    cadastro.
  - *Eliminar* exige digitar `ELIMINAR` e é irreversível. Apaga identificação,
    imagens e textos livres (observações). Ficam, sem identificação, os termos,
    a situação diagnóstica e os resultados (RN08). A coluna nova
    `digital.arquivo_eliminado_em` marca as imagens apagadas.
  - Cada ação gera um registro na auditoria (`PACIENTE_EXPORTADO`,
    `IDENTIFICACAO_CORRIGIDA`, `PACIENTE_ELIMINADO`), sem valores pessoais.

## Opções consideradas

| Opção | Prós | Contras |
|---|---|---|
| Administrador sem acesso; o médico atende a UC12 | Menor exposição dos dados | Contraria o doc 03; depende do médico estar disponível para cada pedido |
| Administrador atende só por identificador, sem listar pacientes | Acesso pontual | Na prática ele vê os dados ao exportar; a busca fica difícil |
| **Administrador vê tudo, sem ações clínicas (escolhida)** | Atende a UC12 e dá supervisão do protocolo; as ações clínicas ficam com quem tem responsabilidade clínica | Mais uma pessoa com acesso a dados de saúde e biométricos |
| Administrador com todas as ações do médico | Mais simples | Mistura papel administrativo e ato médico |

## Consequências

- Aumenta o número de pessoas com acesso a dados sensíveis. Pela LGPD (art. 6º,
  III, necessidade), esse acesso precisa estar previsto no protocolo do CEP e no
  TCLE, e o papel de administrador deve ficar com o menor número possível de
  pessoas da equipe.
- A auditoria passa a ser o controle principal sobre o administrador. Ela é
  somente inserção, e a aplicação não tem caminho para alterá-la.
- O arquivo exportado sai do sistema com dados pessoais e biométricos. A tela
  avisa que ele deve ser entregue por meio seguro e a cópia local apagada.
- **Revisitar** quando o protocolo do CEP definir quem atende os pedidos do
  titular: se for uma pessoa que não seja da equipe, pode valer um papel próprio,
  só para a UC12.

## Ações

1. [x] Políticas por ação na API (`MedicoOuAdmin`, `ApenasMedico`, `ApenasAdmin`) e
   escopo de leitura do administrador nos serviços.
2. [x] UC12 na API e na página do paciente (front).
3. [ ] Prever no protocolo do CEP e no TCLE o acesso do administrador e o fluxo
   dos pedidos do titular.
