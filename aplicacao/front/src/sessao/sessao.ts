import type { Papel, RespostaEntrada } from '../api/autenticacao';

/** O mínimo para manter a sessão (ADR-0007): nenhum dado de paciente. */
export interface Sessao {
  token: string;
  expiraEm: number; // epoch em ms
  usuario: { id: string; nome: string; papel: Papel };
}

export const CHAVE_SESSAO = 'dermatoglifia.sessao';

export function sessaoDaResposta(resposta: RespostaEntrada): Sessao {
  return { token: resposta.token, expiraEm: Date.parse(resposta.expiraEm), usuario: resposta.usuario };
}

/** Lê a sessão da aba; descarta se estiver corrompida ou expirada. */
export function lerSessaoGuardada(agora: number = Date.now()): Sessao | null {
  let bruto: string | null;
  try {
    bruto = sessionStorage.getItem(CHAVE_SESSAO);
  } catch {
    return null; // armazenamento bloqueado pelo navegador
  }

  if (!bruto) {
    return null;
  }

  try {
    const valor: unknown = JSON.parse(bruto);
    if (ehSessao(valor) && valor.expiraEm > agora) {
      return valor;
    }
  } catch {
    // JSON inválido: tratado como sem sessão
  }

  apagarSessao();
  return null;
}

export function guardarSessao(sessao: Sessao): void {
  try {
    sessionStorage.setItem(CHAVE_SESSAO, JSON.stringify(sessao));
  } catch {
    // sem armazenamento a sessão vale só até recarregar a página
  }
}

export function apagarSessao(): void {
  try {
    sessionStorage.removeItem(CHAVE_SESSAO);
  } catch {
    // nada a apagar
  }
}

function ehSessao(valor: unknown): valor is Sessao {
  if (typeof valor !== 'object' || valor === null) return false;
  const s = valor as Record<string, unknown>;
  const usuario = s.usuario as Record<string, unknown> | null | undefined;
  return (
    typeof s.token === 'string' &&
    typeof s.expiraEm === 'number' &&
    typeof usuario === 'object' &&
    usuario !== null &&
    typeof usuario.id === 'string' &&
    typeof usuario.nome === 'string' &&
    (usuario.papel === 'MEDICO' || usuario.papel === 'ADMIN')
  );
}
