import type { ReactNode } from 'react';
import estilos from './Mensagem.module.css';

interface PropsMensagem {
  tipo?: 'erro' | 'informacao';
  titulo?: string;
  children: ReactNode;
}

/** Erros são anunciados na hora (role="alert"); informações, com educação (role="status"). */
export function Mensagem({ tipo = 'informacao', titulo, children }: PropsMensagem) {
  return (
    <div role={tipo === 'erro' ? 'alert' : 'status'} className={`${estilos.mensagem} ${estilos[tipo]}`}>
      {titulo && <p className={estilos.titulo}>{titulo}</p>}
      <div>{children}</div>
    </div>
  );
}
