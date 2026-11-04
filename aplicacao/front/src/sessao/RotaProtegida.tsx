import { Navigate, Outlet, useLocation } from 'react-router';
import type { Papel } from '../api/autenticacao';
import { AcessoNegado } from '../paginas/AcessoNegado';
import { useSessao } from './contextoSessao';

/** Estado passado para /entrar: para onde voltar depois do login. */
export interface EstadoEntrar {
  de?: string;
}

/**
 * Sem sessão → /entrar (voltando depois para a página pedida).
 * Papel diferente → "Acesso não permitido", sem revelar o conteúdo.
 */
export function RotaProtegida({ papel }: { papel?: Papel }) {
  const { sessao } = useSessao();
  const local = useLocation();

  if (!sessao) {
    const estado: EstadoEntrar = { de: `${local.pathname}${local.search}` };
    return <Navigate to="/entrar" replace state={estado} />;
  }

  if (papel && sessao.usuario.papel !== papel) {
    return <AcessoNegado />;
  }

  return <Outlet />;
}
