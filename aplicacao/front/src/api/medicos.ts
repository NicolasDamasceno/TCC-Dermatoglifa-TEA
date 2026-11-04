import type { ClienteApi } from './cliente';

/** POST /api/medicos/solicitacoes (UC02). */
export interface DadosSolicitacao {
  nome: string;
  email: string;
  senha: string;
  crm: string;
  ufCrm: string;
  especialidade: string;
}

/** Item de GET /api/medicos/solicitacoes (UC03, só administrador). */
export interface SolicitacaoPendente {
  id: string;
  nome: string;
  email: string;
  crm: string;
  ufCrm: string;
  especialidade: string;
  solicitadoEm: string;
}

export const CHAVE_PENDENTES = ['medicos', 'solicitacoes'] as const;

export function solicitarCadastro(api: ClienteApi, dados: DadosSolicitacao): Promise<{ id: string; status: string }> {
  return api.requisitar('POST', '/api/medicos/solicitacoes', dados);
}

export function listarPendentes(api: ClienteApi): Promise<SolicitacaoPendente[]> {
  return api.requisitar('GET', '/api/medicos/solicitacoes');
}

export function aprovarCadastro(api: ClienteApi, id: string, vinculoProtocolo: string): Promise<void> {
  return api.requisitar('POST', `/api/medicos/${encodeURIComponent(id)}/aprovacao`, { vinculoProtocolo });
}

export function recusarCadastro(api: ClienteApi, id: string, motivo: string): Promise<void> {
  return api.requisitar('POST', `/api/medicos/${encodeURIComponent(id)}/recusa`, { motivo });
}
