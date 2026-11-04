import { createContext, useContext } from 'react';
import type { RespostaEntrada } from '../api/autenticacao';
import type { ClienteApi } from '../api/cliente';
import type { Sessao } from './sessao';

/** Por que a última sessão terminou — a tela Entrar explica ao médico. */
export type MotivoSaida = 'expirou' | 'saiu';

export interface ValorSessao {
  sessao: Sessao | null;
  api: ClienteApi;
  motivoSaida: MotivoSaida | null;
  iniciar: (resposta: RespostaEntrada) => void;
  sair: (motivo: MotivoSaida) => void;
}

export const ContextoSessao = createContext<ValorSessao | null>(null);

export function useSessao(): ValorSessao {
  const valor = useContext(ContextoSessao);
  if (!valor) {
    throw new Error('useSessao precisa estar dentro de <SessaoProvider>.');
  }

  return valor;
}

/** Atalho para telas que só existem com usuário logado. */
export function useSessaoAtiva(): Sessao {
  const { sessao } = useSessao();
  if (!sessao) {
    throw new Error('Tela protegida renderizada sem sessão.');
  }

  return sessao;
}
