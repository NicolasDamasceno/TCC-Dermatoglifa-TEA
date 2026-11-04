import { Link } from 'react-router';
import { useTituloPagina } from '../componentes/useTituloPagina';
import estilos from './Paginas.module.css';

export function NaoEncontrada() {
  useTituloPagina('Página não encontrada');
  return (
    <section className={estilos.simples} aria-labelledby="titulo-pagina">
      <h1 id="titulo-pagina">Página não encontrada</h1>
      <p>O endereço não existe. Confira o link ou volte para a página inicial.</p>
      <p>
        <Link to="/">Ir para a página inicial</Link>
      </p>
    </section>
  );
}
