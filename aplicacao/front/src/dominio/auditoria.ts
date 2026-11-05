/** Nomes por extenso das ações registradas na trilha (UC11). Ação desconhecida aparece pelo código. */
const ACOES: Readonly<Record<string, string>> = {
  LOGIN_OK: 'Entrou no sistema',
  LOGIN_FALHA: 'Tentativa de entrada com credenciais inválidas',
  LOGIN_BLOQUEADO: 'Tentativa de entrada com a conta bloqueada',
  LOGIN_CONTA_NAO_ATIVA: 'Tentativa de entrada com cadastro não ativo',
  ADMIN_INICIAL_CRIADO: 'Administrador inicial criado',
  MEDICO_SOLICITOU_CADASTRO: 'Médico pediu cadastro',
  MEDICO_APROVADO: 'Cadastro de médico aprovado',
  MEDICO_RECUSADO: 'Cadastro de médico recusado',
  PACIENTE_CRIADO: 'Paciente cadastrado',
  PACIENTE_VISUALIZADO: 'Paciente aberto',
  PACIENTES_LISTADOS: 'Lista de pacientes aberta',
  CONSENTIMENTO_REGISTRADO: 'Consentimento registrado',
  CONSENTIMENTO_REVOGADO: 'Consentimento revogado',
  SITUACAO_DIAGNOSTICA_ATUALIZADA: 'Situação diagnóstica atualizada',
  SESSAO_INICIADA: 'Coleta iniciada',
  DIGITAL_ENVIADA: 'Digital enviada',
  DEDO_NAO_COLETAVEL: 'Dedo marcado como não coletável',
  SESSAO_CONCLUIDA: 'Coleta concluída',
  SESSAO_CANCELADA: 'Coleta cancelada',
  ANALISE_SOLICITADA: 'Análise solicitada',
  ANALISE_CONCLUIDA: 'Análise concluída',
  ANALISE_ERRO: 'Análise com erro',
  ANALISE_NOVA_TENTATIVA: 'Análise repetida',
  ANALISE_VISUALIZADA: 'Resultado aberto',
  ANALISE_OBSERVACAO_REGISTRADA: 'Observação do médico salva',
  ANALISE_PRONTUARIO_CONFIRMADO: 'Registro no prontuário confirmado',
  MODELO_PROMOVIDO: 'Versão do modelo promovida',
  AUDITORIA_CONSULTADA: 'Trilha de auditoria consultada',
};

export function nomeDaAcao(acao: string): string {
  return ACOES[acao] ?? acao;
}

const ENTIDADES: Readonly<Record<string, string>> = {
  paciente: 'Paciente',
  sessao_coleta: 'Coleta',
  analise: 'Análise',
  medico: 'Médico',
  modelo_ia: 'Modelo de IA',
  usuario: 'Usuário',
  log_auditoria: 'Trilha de auditoria',
};

export function nomeDaEntidade(entidade: string): string {
  return ENTIDADES[entidade] ?? entidade;
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function ehIdentificador(texto: string): boolean {
  return UUID.test(texto);
}

/** Início do dia local (AAAA-MM-DD) como instante ISO; com `diaSeguinte`, o início do dia seguinte. */
export function inicioDoDia(dia: string, diaSeguinte = false): string | undefined {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dia);
  if (!m) return undefined;
  const data = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]) + (diaSeguinte ? 1 : 0));
  return data.toISOString();
}
