import type { Indicadores as DadosIndicadores } from '../../api/analises';
import estilos from './Resultado.module.css';

interface Linha {
  nome: string;
  valor: number;
  escala: string;
  descricao: string;
}

/**
 * Indicadores derivados (ADR-0002) com o que cada um mede. Nenhuma linha diz
 * "alto" ou "baixo": não existe ponto de corte validado (RN04).
 */
export function Indicadores({ indicadores: i }: { indicadores: DadosIndicadores }) {
  const contagens: Linha[] = [
    { nome: 'Verticilos', valor: i.qtdVerticilos, escala: 'de 10', descricao: 'Dedos com padrão em espiral ou círculos.' },
    { nome: 'Alças ulnares', valor: i.qtdAlcasUlnares, escala: 'de 10', descricao: 'Alças abertas para o lado do dedo mínimo.' },
    { nome: 'Alças radiais', valor: i.qtdAlcasRadiais, escala: 'de 10', descricao: 'Alças abertas para o lado do polegar.' },
    { nome: 'Arcos', valor: i.qtdArcos, escala: 'de 10', descricao: 'Cristas que atravessam o dedo sem voltar.' },
    { nome: 'Arcos tendidos', valor: i.qtdArcosTendidos, escala: 'de 10', descricao: 'Arcos com elevação central acentuada.' },
  ];
  const discordancias: Linha[] = [
    {
      nome: 'Discordância bilateral',
      valor: i.discordanciaBilateral,
      escala: 'de 4',
      descricao:
        'Pares de dedos homólogos (indicador, médio, anelar e mínimo) com padrões diferentes entre as mãos, polegar excluído, como em de Bruin et al. (2014).',
    },
    {
      nome: 'Discordância bilateral com polegar',
      valor: i.discordanciaBilateralComPolegar,
      escala: 'de 5',
      descricao: 'A mesma contagem incluindo o par de polegares.',
    },
  ];

  return (
    <section className={estilos.secao} aria-labelledby="titulo-indicadores">
      <h2 id="titulo-indicadores">Indicadores</h2>
      <div className={estilos.rolagem} role="region" aria-label="Tabela de indicadores" tabIndex={0}>
        <table className={estilos.indicadores}>
          <thead>
            <tr>
              <th scope="col">Indicador</th>
              <th scope="col" className={estilos.colValor}>
                Valor
              </th>
              <th scope="col" className={estilos.colDescricao}>
                O que conta
              </th>
            </tr>
          </thead>
          <tbody>
            {[...contagens, ...discordancias].map((l, indice) => (
              <tr key={l.nome} className={indice === contagens.length ? estilos.inicioGrupo : undefined}>
                <th scope="row">
                  {l.nome}
                  <span className={estilos.descricaoCompacta}>{l.descricao}</span>
                </th>
                <td className={estilos.colValor}>
                  <span className={estilos.valor}>{l.valor}</span> <span className={estilos.escala}>{l.escala}</span>
                </td>
                <td className={estilos.descricao}>{l.descricao}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
