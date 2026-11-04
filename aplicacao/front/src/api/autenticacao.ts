import type { ClienteApi } from './cliente';

export type Papel = 'MEDICO' | 'ADMIN';

/** Resposta de POST /api/auth/login (doc 08, seção 5). */
export interface RespostaEntrada {
  token: string;
  expiraEm: string;
  usuario: { id: string; nome: string; papel: Papel };
}

export function entrar(api: ClienteApi, email: string, senha: string): Promise<RespostaEntrada> {
  return api.requisitar<RespostaEntrada>('POST', '/api/auth/login', { email, senha });
}
