# ADR-0004: Captura das digitais por envio de imagem, um dedo por vez

**Status:** Proposto
**Data:** 2026-10-20
**Decisores:** Nicolas Damasceno (autor), orientador do TCC

## Contexto

O médico precisa registrar as 10 digitais do paciente pelo site (React). Pontos
que restringem a escolha (fontes em [`../07-fundamentos-pesquisa.md`](../07-fundamentos-pesquisa.md)):

- Navegadores não acessam leitores biométricos USB diretamente; integrar o SDK de
  um leitor exige um programa local instalado na máquina do médico.
- Os estudos de TEA capturaram digitais com tinta e scanner de mesa (1800 dpi em
  de Bruin et al. 2014) ou com leitor óptico USB (Futronic FS80 em Kazemi et al. 2017).
- O padrão ANSI/NIST-ITL 1-2000 fixa **500 ppi** como resolução mínima de
  digitalização (seção 5.1) e pixels em **8 bits de cinza** (seção 4.2)
  ([NIST SP 500-245](https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication500-245.pdf)).
- Capturas planas costumam cortar os deltas de alças e verticilos; capturas
  **roladas** (de borda a borda da unha) preservam essa região, necessária para
  distinguir padrões e, no futuro, contar cristas.
- **As imagens do treino e as da produção precisam vir do mesmo tipo de captura.**
  Um modelo treinado com tinta escaneada e usado com leitor óptico (ou o
  contrário) enfrenta mudança de distribuição e pode errar sem aviso.

## Decisão

No MVP, o front-end envia **um arquivo de imagem por dedo** (upload), obtido por
um único método de captura definido no protocolo de pesquisa. O back-end:

- aceita PNG, BMP ou TIFF sem perda (JPEG rejeitado, para não introduzir
  artefatos de compressão nas cristas);
- exige no mínimo **500 ppi** e tamanho máximo configurável (ex.: 5 MB);
- converte para 8 bits em tons de cinza antes de armazenar;
- registra `metodo_captura` na sessão e `resolucao_dpi` em cada digital.

O método preferido é **leitor óptico com captura rolada**; a alternativa é tinta
em ficha e scanner de mesa. O protocolo do CEP deve fixar um deles para todo o
estudo.

## Opções consideradas

### Opção A: envio de imagem por dedo (escolhida para o MVP)
| Dimensão | Avaliação |
|---|---|
| Complexidade | Baixa |
| Custo | Leitor óptico simples ou scanner de mesa já disponível |
| Compatibilidade | Qualquer navegador; independe do fabricante do leitor |

**Prós:** funciona com qualquer leitor que salve arquivo e com tinta escaneada;
mantém o front simples.
**Contras:** mais cliques para o médico; risco de trocar a imagem de um dedo por
outro (mitigado pela ordem fixa e pela pré-visualização antes de confirmar).

### Opção B: integração direta com SDK do leitor (agente local)
**Prós:** captura guiada, verificação de qualidade em tempo real.
**Contras:** depende de um fabricante específico, exige instalar software na
máquina do médico e programar fora do escopo .NET/React do TCC.

### Opção C: câmera do celular (sem contato)
**Prós:** dispensa leitor.
**Contras:** iluminação, foco e distorção variáveis; nenhum dos estudos de TEA
levantados usou esse método, o que afasta os dados da literatura de referência.

## Consequências

- Mais fácil: começar sem hardware específico; reaproveitar o mesmo formato de
  arquivo no treino local.
- Mais difícil: garantir qualidade sem verificação em tempo real.
- Revisitar: adicionar filtro de qualidade com o **NFIQ 2** (padrão aberto do
  NIST) no Serviço de IA; avaliar a Opção B se o leitor for definido.

## Ações

1. [ ] Definir com o orientador o leitor ou o procedimento de tinta, e registrá-lo no protocolo do CEP.
2. [ ] Implementar a validação de formato/resolução no back-end com testes.
3. [ ] Confirmar a orientação das imagens do método escolhido para a tabela alça esquerda/direita → ulnar/radial (ADR-0002).
