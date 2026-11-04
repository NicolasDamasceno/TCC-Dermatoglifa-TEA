import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router';
import { vi } from 'vitest';
import type { Papel, RespostaEntrada } from '../api/autenticacao';
import { App } from '../App';
import { CHAVE_SESSAO, type Sessao } from '../sessao/sessao';
import { SessaoProvider } from '../sessao/SessaoProvider';

/** Usuários sintéticos — testes nunca usam dados reais (CLAUDE.md, regra 4). */
export const MEDICO = { id: '00000000-0000-0000-0000-000000000001', nome: 'Dra. Teste Sintética', papel: 'MEDICO' as Papel };
export const ADMIN = { id: '00000000-0000-0000-0000-000000000002', nome: 'Admin Sintético', papel: 'ADMIN' as Papel };

export function respostaEntrada(usuario = MEDICO, validadeMs = 60 * 60 * 1000): RespostaEntrada {
  return { token: `token-${usuario.papel}`, expiraEm: new Date(Date.now() + validadeMs).toISOString(), usuario };
}

/** Grava uma sessão válida na aba, como se o usuário já tivesse entrado. */
export function comSessao(usuario = MEDICO, validadeMs = 60 * 60 * 1000): Sessao {
  const sessao: Sessao = { token: `token-${usuario.papel}`, expiraEm: Date.now() + validadeMs, usuario };
  sessionStorage.setItem(CHAVE_SESSAO, JSON.stringify(sessao));
  return sessao;
}

export function json(status: number, corpo: unknown): Response {
  return new Response(JSON.stringify(corpo), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
  });
}

/** Troca o fetch global por respostas em fila; devolve o mock para inspecionar as chamadas. */
export function simularFetch(...respostas: (Response | Error)[]) {
  const fila = [...respostas];
  const mock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() => {
    const proxima = fila.shift();
    if (!proxima) return Promise.reject(new Error('fetch inesperado no teste'));
    return proxima instanceof Error ? Promise.reject(proxima) : Promise.resolve(proxima);
  });
  vi.stubGlobal('fetch', mock);
  return mock;
}

export function Provedores({ children, rota = '/' }: { children: ReactNode; rota?: string }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[rota]}>
        <SessaoProvider>{children}</SessaoProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

export function renderizarApp(rota: string) {
  return render(
    <Provedores rota={rota}>
      <App />
    </Provedores>,
  );
}
