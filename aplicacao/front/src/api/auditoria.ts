import type { ClienteApi } from './cliente';
import type { Papel } from './autenticacao';

export interface UsuarioAuditoria {
  id: string;
  nome: string;
  email: string;
  papel: Papel;
}

/** Um evento da trilha (UC11). `detalhes` só tem campos permitidos: nunca nome, imagem ou senha. */
export interface RegistroAuditoria {
  id: number;
  ocorridoEm: string;
  usuarioId: string | null;
  usuario: UsuarioAuditoria | null;
  acao: string;
  entidade: string;
  entidadeId: string | null;
  ip: string | null;
  detalhes: Record<string, string> | null;
}

export interface PaginaAuditoria {
  itens: RegistroAuditoria[];
  total: number;
  pagina: number;
  tamanho: number;
}

export interface FiltrosAuditoria {
  acoes: string[];
  usuarios: UsuarioAuditoria[];
}

/** Filtros como a API recebe: período em instantes (ISO 8601), `ate` exclusivo. */
export interface ConsultaAuditoria {
  usuarioId?: string;
  pacienteId?: string;
  acao?: string;
  de?: string;
  ate?: string;
  pagina: number;
}

export const chavesAuditoria = {
  filtros: ['auditoria', 'filtros'] as const,
  consulta: (c: ConsultaAuditoria) => ['auditoria', 'consulta', c] as const,
};

export function consultarAuditoria(api: ClienteApi, c: ConsultaAuditoria): Promise<PaginaAuditoria> {
  const parametros = new URLSearchParams();
  for (const [chave, valor] of Object.entries(c)) {
    if (valor !== undefined && valor !== '') parametros.set(chave, String(valor));
  }
  return api.requisitar('GET', `/api/auditoria?${parametros.toString()}`);
}

export function obterFiltrosAuditoria(api: ClienteApi): Promise<FiltrosAuditoria> {
  return api.requisitar('GET', '/api/auditoria/filtros');
}
