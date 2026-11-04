import type { ButtonHTMLAttributes } from 'react';
import estilos from './Botao.module.css';

interface PropsBotao extends ButtonHTMLAttributes<HTMLButtonElement> {
  variante?: 'principal' | 'secundario';
  /** Enquanto true, o botão fica desabilitado e mostra `textoCarregando`. */
  carregando?: boolean;
  textoCarregando?: string;
}

export function Botao({
  variante = 'principal',
  carregando = false,
  textoCarregando,
  disabled,
  className,
  children,
  type = 'button',
  ...resto
}: PropsBotao) {
  const classes = [estilos.botao, estilos[variante], className].filter(Boolean).join(' ');
  return (
    <button {...resto} type={type} className={classes} disabled={disabled ?? carregando} aria-busy={carregando || undefined}>
      {carregando && textoCarregando ? textoCarregando : children}
    </button>
  );
}
