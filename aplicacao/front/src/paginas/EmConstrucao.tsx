import { useTituloPagina } from '../componentes/useTituloPagina';
import estilos from './Paginas.module.css';

interface PropsEmConstrucao {
  titulo: string;
  marco: string;
  descricao: string;
}

/** Tela reservada para um marco seguinte do front (doc 09, seção 10). */
export function EmConstrucao({ titulo, marco, descricao }: PropsEmConstrucao) {
  useTituloPagina(titulo);
  return (
    <section className={estilos.simples} aria-labelledby="titulo-pagina">
      <h1 id="titulo-pagina">{titulo}</h1>
      <p>{descricao}</p>
      <p className={estilos.nota}>Esta tela entra no marco {marco} do front-end.</p>
    </section>
  );
}
