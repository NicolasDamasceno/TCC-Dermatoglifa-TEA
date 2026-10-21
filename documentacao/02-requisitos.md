# 02 — Requisitos

Prioridade: **M** = MVP, **P** = pós-MVP. A base normativa citada nos requisitos
não funcionais está detalhada em [07-fundamentos-pesquisa.md](07-fundamentos-pesquisa.md);
os controles são boas práticas de engenharia justificadas por essas normas
(nenhuma norma brasileira localizada fixa algoritmos ou parâmetros técnicos).

## Requisitos funcionais

| ID | Requisito | Caso de uso | Prior. |
|---|---|---|---|
| RF01 | Autenticar médicos e administradores por e-mail e senha, emitindo token com o papel do usuário | UC01 | M |
| RF02 | Permitir que um médico solicite cadastro informando nome, e-mail, senha, CRM, UF e especialidade | UC02 | M |
| RF03 | Permitir que o administrador aprove (registrando o vínculo com o protocolo) ou recuse cadastros pendentes | UC03 | M |
| RF04 | Cadastrar paciente gravando separadamente o registro pseudônimo e os dados de identificação | UC04 | M |
| RF05 | Registrar TCLE/TALE com versão, data, autorização da análise, ciência do uso de IA e autorização (opcional) para treino; permitir revogação | UC05 | M |
| RF06 | Conduzir a coleta das 10 digitais em ordem fixa (mão direita, depois esquerda; polegar a mínimo), com envio e substituição por dedo | UC06 | M |
| RF07 | Registrar dedo não coletável com motivo, encerrando a sessão como incompleta | UC06 | M |
| RF08 | Ao concluir a sessão completa, solicitar a análise ao Serviço de IA e gravar padrões por dedo, indicadores, escore (ou nulo) e versão do modelo; permitir nova tentativa em caso de erro | UC07 | M |
| RF09 | Exibir o resultado com aviso fixo, padrões por dedo, indicadores, versão e métricas do modelo, alertas de qualidade; permitir observação e confirmação de registro no prontuário | UC08 | M |
| RF10 | Listar os pacientes do médico e o histórico de sessões e análises de cada um | UC09 | P |
| RF11 | Registrar a promoção de uma versão de modelo (arquivo, hash, métricas), mantendo só uma ativa | UC10 | P |
| RF12 | Consultar a trilha de auditoria com filtros | UC11 | P |
| RF13 | Exportar, corrigir ou eliminar dados de um titular e registrar revogação | UC12 | P |
| RF14 | Atualizar a situação diagnóstica do paciente e sua fonte | UC13 | P |
| RF15 | Exportar dados pseudonimizados para treino, apenas de quem autorizou | UC14 | P |

## Regras de negócio

| ID | Regra | Origem |
|---|---|---|
| RN01 | Só acessam o sistema usuários com cadastro **ativo**; médicos só são aprovados se vinculados ao protocolo aprovado pelo CEP | RDC 751/2022, art. 2º, §8º (isenção só dentro da investigação) |
| RN02 | A análise só é solicitada com as 10 digitais coletadas | ADR-0002 (discordância bilateral exige os pares completos) |
| RN03 | Nenhuma digital é coletada sem TCLE vigente com autorização da análise e ciência do uso de IA; para menores, também o TALE quando a criança puder compreender | Res. CNS 466/2012; Lei 14.874/2024; LGPD art. 14; Res. CFM 2.454/2026 |
| RN04 | Todo resultado é exibido com o aviso de protótipo de pesquisa, sem ponto de corte validado e, para pacientes do sexo feminino, com a ressalva de falta de dados na literatura | doc 07, seção de evidência |
| RN05 | Um médico só acessa pacientes pelos quais é responsável | LGPD art. 6º, III e art. 46 |
| RN06 | Toda análise registra a versão do modelo que a produziu | Anvisa Q50 (RDC 657); rastreabilidade |
| RN07 | Digitais só entram em treino/validação de modelos com autorização específica para esse fim | Res. CNS 738/2024, art. 19; LGPD art. 11, I |
| RN08 | O pedido do titular de eliminação apaga identificação e imagens, mantendo apenas indicadores agregados pseudônimos | LGPD arts. 16 e 18 |
| RN09 | Sessão com dedo não coletável não é analisada no MVP | ADR-0002 |
| RN10 | O escore só é exibido depois que a Etapa 2 for validada; até lá, só os indicadores | ADR-0002; doc 07 (nenhuma acurácia publicada) |

## Requisitos não funcionais

| ID | Categoria | Requisito | Base | Prior. |
|---|---|---|---|---|
| RNF01 | Qualidade de dados | Aceitar só PNG, BMP ou TIFF, ≥ 500 ppi, até 5 MB; converter para 8 bits em cinza | ANSI/NIST-ITL 1-2000 §5.1 e §4.2; ADR-0004 | M |
| RNF02 | Segurança | Imagens cifradas em repouso (AES-256-GCM por arquivo), chave fora do banco e do código; diretório fora da pasta pública | LGPD arts. 13 e 46; Res. CNS 738/2024, art. 4º | M |
| RNF03 | Segurança | HTTPS (TLS 1.2+) entre front e API; token JWT com expiração curta | LGPD art. 46 | M |
| RNF04 | Desempenho | Análise de uma sessão concluída em até 30 s em CPU; timeout configurável na chamada ao Serviço de IA | ADR-0001 | M |
| RNF05 | Segurança | Senhas com hash adaptativo; bloqueio temporário após 5 falhas seguidas; mensagens de erro genéricas | Boa prática (Guia ANPD de segurança) | M |
| RNF06 | Privacidade | Identificação do paciente em schema separado; Serviço de IA recebe só imagens, mão, dedo, sexo e idade em meses | LGPD art. 13, §4º; Lei 14.874/2024, art. 51, §1º | M |
| RNF07 | Auditoria | Log somente-inserção de acessos e alterações, sem dado biométrico ou nome no conteúdo | LGPD art. 37; Res. CNS 738/2024 ("rastreável") | M |
| RNF08 | Sigilo do repositório | Nenhum dado real, imagem de participante, segredo ou peso treinado com dados reais no Git; `.gitignore` + hooks; testes só com dados sintéticos | LGPD art. 46 | M |
| RNF09 | Rastreabilidade de IA | Cada versão de modelo com hash, configuração, métricas de teste e notas versionadas | Anvisa Q50; Res. CFM 2.454/2026 | M |
| RNF10 | Retenção | Guarda mínima de 5 anos após o estudo, depois eliminação ou anonimização, conforme protocolo | Res. CNS 466/2012, XI.2 "f"; Lei 14.874/2024, art. 51; LGPD art. 16 | P |
| RNF11 | Máquina de treino | Disco cifrado (BitLocker/LUKS), sem sincronização com nuvem pessoal, backup cifrado | LGPD art. 13 ("ambiente controlado e seguro") | M |
| RNF12 | Autenticação forte | Segundo fator (TOTP) para todos os usuários | Boa prática | P |
| RNF13 | Usabilidade | Coleta guiada com pré-visualização de cada dedo antes de confirmar; interface responsiva | ADR-0004 (evitar troca de dedos) | M |
| RNF14 | Manutenibilidade | Testes automatizados para regras de domínio, cálculo de indicadores e conversão ulnar/radial | ADR-0002 | M |
| RNF15 | Portabilidade | Ambiente de desenvolvimento reproduzível com `docker compose` (PostgreSQL + Serviço de IA) | doc 06 | P |

## Rastreabilidade requisito → artefato

| Requisito | Documento |
|---|---|
| RF04, RNF06 | doc 05 (`paciente`, `identificacao_paciente`) |
| RF05, RN03, RN07 | doc 05 (`consentimento`), doc 04 (seq. 4) |
| RF06–RF08, RN02 | doc 04 (seq. 5, estados), ADR-0001, ADR-0002, ADR-0004 |
| RF09, RN04, RN10 | doc 03 (UC08), doc 06 (componente `AvisoNaoDiagnostico`) |
| RNF01 | ADR-0004 |
| RNF08 | `CLAUDE.md` (Regra 4), `.gitignore`, `.githooks/` |
