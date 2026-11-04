import type { ResultadoDedo } from '../../api/analises';
import { DEDOS, MAOS, NOME_DEDO, NOME_MAO } from '../../dominio/dedos';
import { FichaDecadactilar } from '../../componentes/FichaDecadactilar';
import { NOME_PADRAO, percentual } from '../../dominio/analises';
import estilos from './Resultado.module.css';

/** A ficha com o padrão previsto e a confiança de cada dedo, sem as imagens (doc 09). */
export function FichaAnotada({ digitais }: { digitais: ResultadoDedo[] }) {
  const algumAlerta = digitais.some((d) => d.alertaQualidade);
  return (
    <section className={estilos.secao} aria-labelledby="titulo-ficha">
      <h2 id="titulo-ficha">Padrão de cada dedo</h2>
      <FichaDecadactilar
        legenda="Padrão previsto e confiança por dedo"
        className={estilos.ficha}
        quadro={(mao, dedo) => {
          const r = digitais.find((d) => d.mao === mao && d.dedo === dedo);
          if (!r) return <span className={estilos.semResultado}>Sem resultado</span>;
          return (
            <div className={estilos.quadroResultado}>
              <span className={estilos.padrao}>{NOME_PADRAO[r.padrao]}</span>
              <span className={estilos.confianca}>confiança {percentual(r.confianca)}</span>
              {r.alertaQualidade && <span className={estilos.qualidade}>Baixa qualidade</span>}
            </div>
          );
        }}
      />
      {/* Em telas estreitas a ficha vira lista por mão (só uma das duas fica visível e acessível). */}
      <div className={estilos.fichaLista}>
        {MAOS.map((mao) => (
          <div key={mao}>
            <h3>Mão {NOME_MAO[mao].toLowerCase()}</h3>
            <ul>
              {DEDOS.map((dedo) => {
                const r = digitais.find((d) => d.mao === mao && d.dedo === dedo);
                return (
                  <li key={dedo}>
                    <span className={estilos.dedoLista}>{NOME_DEDO[dedo]}</span>
                    {r ? (
                      <span>
                        <strong>{NOME_PADRAO[r.padrao]}</strong>, confiança {percentual(r.confianca)}
                        {r.alertaQualidade && <span className={estilos.qualidade}>Baixa qualidade</span>}
                      </span>
                    ) : (
                      <span className={estilos.semResultado}>Sem resultado</span>
                    )}
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </div>
      <p className={estilos.nota}>
        Alças já convertidas em ulnar ou radial conforme a mão. Confiança é a probabilidade que o modelo atribuiu ao
        padrão previsto, não a chance de TEA.
        {algumAlerta && ' Dedos com baixa qualidade de imagem podem ter o padrão errado.'}
      </p>
    </section>
  );
}
