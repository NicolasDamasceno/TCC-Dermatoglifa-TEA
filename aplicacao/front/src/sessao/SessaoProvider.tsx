import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { RespostaEntrada } from '../api/autenticacao';
import { criarClienteApi } from '../api/cliente';
import { ContextoSessao, type MotivoSaida, type ValorSessao } from './contextoSessao';
import { apagarSessao, guardarSessao, lerSessaoGuardada, sessaoDaResposta, type Sessao } from './sessao';

const URL_API = (import.meta.env.VITE_API_URL as string | undefined) ?? '';

/**
 * Guarda a sessão da aba (ADR-0007): sai sozinho quando o token expira e em
 * qualquer 401 da API; ao sair, limpa todo dado de paciente em cache.
 */
export function SessaoProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [sessao, setSessao] = useState<Sessao | null>(() => lerSessaoGuardada());
  const [motivoSaida, setMotivoSaida] = useState<MotivoSaida | null>(null);

  const sair = useCallback(
    (motivo: MotivoSaida) => {
      apagarSessao();
      queryClient.clear();
      setSessao(null);
      setMotivoSaida(motivo);
    },
    [queryClient],
  );

  const iniciar = useCallback((resposta: RespostaEntrada) => {
    const nova = sessaoDaResposta(resposta);
    guardarSessao(nova);
    setMotivoSaida(null);
    setSessao(nova);
  }, []);

  // Um cliente por token: ao entrar ou sair, as telas passam a usar o novo.
  const token = sessao?.token ?? null;
  const api = useMemo(
    () =>
      criarClienteApi({
        urlBase: URL_API,
        obterToken: () => token,
        aoPerderSessao: () => {
          sair('expirou');
        },
      }),
    [token, sair],
  );

  // Sai sozinho no instante em que o token expira.
  useEffect(() => {
    if (!sessao) {
      return undefined;
    }

    const restante = sessao.expiraEm - Date.now();
    const temporizador = setTimeout(() => {
      sair('expirou');
    }, Math.max(restante, 0));
    return () => {
      clearTimeout(temporizador);
    };
  }, [sessao, sair]);

  const valor = useMemo<ValorSessao>(
    () => ({ sessao, api, motivoSaida, iniciar, sair }),
    [sessao, api, motivoSaida, iniciar, sair],
  );

  return <ContextoSessao.Provider value={valor}>{children}</ContextoSessao.Provider>;
}
