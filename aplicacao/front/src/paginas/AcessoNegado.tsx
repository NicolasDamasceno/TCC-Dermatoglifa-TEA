import { Link } from 'react-router';
import { useTituloPagina } from '../componentes/useTituloPagina';
import estilos from './Paginas.module.css';

export function AcessoNegado() {
  useTituloPagina('Acesso não permitido');
  return (
    <section className={estilos.simples} aria-labelledby="titulo-pagina">
      <h1 id="titulo-pagina">Acesso não permitido</h1>
      <p>Esta página não está disponível para o seu perfil.</p>
      <p>
        <Link to="/">Ir para a página inicial</Link>
      </p>
    </section>
  );
}
