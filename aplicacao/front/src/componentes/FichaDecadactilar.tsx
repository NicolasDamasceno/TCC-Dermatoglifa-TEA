import type { ReactNode } from 'react';
import { DEDOS, MAOS, NOME_DEDO, NOME_MAO, type Dedo, type Mao } from '../dominio/dedos';
import estilos from './FichaDecadactilar.module.css';

interface PropsFicha {
  /** Legenda da tabela; obrigatória quando a ficha não é decorativa. */
  legenda?: string;
  /** Conteúdo de cada quadro (imagem, estado, padrão). Vazio = quadro em branco. */
  quadro?: (mao: Mao, dedo: Dedo) => ReactNode;
  /** Só ilustração: escondida de leitores de tela. */
  decorativa?: boolean;
  /** Quadros ocupados por botões: sem respiro interno, o botão preenche o quadro. */
  interativa?: boolean;
  className?: string;
}

/**
 * A ficha decadactilar (doc 09, seção 2): mão direita em cima, esquerda embaixo,
 * do polegar ao mínimo. É uma tabela de verdade — linha = mão, coluna = dedo —
 * para que o leitor de tela anuncie "Direita, Indicador" em cada quadro.
 */
export function FichaDecadactilar({ legenda, quadro, decorativa = false, interativa = false, className }: PropsFicha) {
  return (
    <table
      className={[estilos.ficha, className].filter(Boolean).join(' ')}
      aria-hidden={decorativa || undefined}
    >
      {legenda && <caption className={estilos.legenda}>{legenda}</caption>}
      <thead>
        <tr>
          <td className={estilos.canto} />
          {DEDOS.map((dedo) => (
            <th key={dedo} scope="col" className={estilos.dedo}>
              {NOME_DEDO[dedo]}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {MAOS.map((mao) => (
          <tr key={mao}>
            <th scope="row" className={estilos.mao}>
              {NOME_MAO[mao]}
            </th>
            {DEDOS.map((dedo) => (
              <td key={dedo} className={interativa ? `${estilos.quadro} ${estilos.interativo}` : estilos.quadro}>
                {quadro?.(mao, dedo)}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  );
}
