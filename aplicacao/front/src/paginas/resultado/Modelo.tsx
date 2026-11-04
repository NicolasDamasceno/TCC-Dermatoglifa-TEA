import type { ModeloAnalise } from '../../api/analises';
import { data } from '../../componentes/formatos';
import { NOME_ORIGEM, nomeMetrica, numero, ordemMetrica } from '../../dominio/analises';
import estilos from './Resultado.module.css';

/** Rastreabilidade (RN06): qual modelo respondeu e como se saiu no conjunto de teste. */
export function Modelo({ modelo: m }: { modelo: ModeloAnalise }) {
  const metricas = Object.entries(m.metricas)
    .filter((par): par is [string, number | string] => typeof par[1] === 'number' || typeof par[1] === 'string')
    .sort(([a], [b]) => ordemMetrica(a) - ordemMetrica(b));

  return (
    <section className={estilos.secao} aria-labelledby="titulo-modelo">
      <h2 id="titulo-modelo">Modelo que respondeu</h2>
      <dl className={estilos.dados}>
        <div>
          <dt>Versão</dt>
          <dd>{m.versao}</dd>
        </div>
        <div>
          <dt>Treinado com</dt>
          <dd>{NOME_ORIGEM[m.origemDados]}</dd>
        </div>
        <div>
          <dt>Treinado em</dt>
          <dd>{data(m.treinadoEm)}</dd>
        </div>
        <div>
          <dt>Escore de risco</dt>
          <dd>{m.escoreValidado ? 'Validado' : 'Não validado'}</dd>
        </div>
      </dl>
      {metricas.length > 0 && (
        <>
          <h3>Métricas no conjunto de teste</h3>
          <dl className={estilos.dados}>
            {metricas.map(([chave, valor]) => (
              <div key={chave}>
                <dt>{nomeMetrica(chave)}</dt>
                <dd className={estilos.metrica}>{typeof valor === 'number' ? numero(valor) : valor}</dd>
              </div>
            ))}
          </dl>
          {!m.escoreValidado && (
            <p className={estilos.nota}>
              Desempenho da classificação dos padrões por dedo. Não mede a capacidade de identificar TEA.
            </p>
          )}
        </>
      )}
    </section>
  );
}
