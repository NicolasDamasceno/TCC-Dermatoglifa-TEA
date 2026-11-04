import { Link, type LinkProps } from 'react-router';
import estilos from './Botao.module.css';

/** Navegação com aparência de botão (ex.: "Cadastrar paciente"): continua sendo um link. */
export function LinkBotao({ variante = 'principal', className, ...resto }: LinkProps & { variante?: 'principal' | 'secundario' }) {
  return <Link {...resto} className={[estilos.botao, estilos[variante], estilos.link, className].filter(Boolean).join(' ')} />;
}
