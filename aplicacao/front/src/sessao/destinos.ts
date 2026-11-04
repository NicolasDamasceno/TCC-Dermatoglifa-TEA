import type { Papel } from '../api/autenticacao';

/** Tela principal de cada papel (doc 09, seção 3). */
export function paginaInicialDo(papel: Papel): string {
  return papel === 'ADMIN' ? '/admin/solicitacoes' : '/pacientes';
}

/** Só caminhos internos ("/pacientes/…"); nada de "//outro-site" nem de voltar ao próprio /entrar. */
export function destinoSeguro(de: unknown): string | undefined {
  if (typeof de !== 'string' || !de.startsWith('/') || de.startsWith('//') || de.startsWith('/entrar')) {
    return undefined;
  }

  return de;
}
