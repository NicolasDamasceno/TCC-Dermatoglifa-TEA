import { Navigate } from 'react-router';
import { useSessaoAtiva } from '../sessao/contextoSessao';
import { paginaInicialDo } from '../sessao/destinos';

/** "/" leva cada papel à sua tela principal. */
export function Inicio() {
  const sessao = useSessaoAtiva();
  return <Navigate to={paginaInicialDo(sessao.usuario.papel)} replace />;
}
