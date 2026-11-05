import { Link } from 'react-router';
import type { SessaoDoPaciente } from '../../api/pacientes';
import { dataHora } from '../../componentes/formatos';
import { NOME_STATUS_ANALISE, NOME_STATUS_SESSAO } from '../../dominio/pacientes';
import estilos from './Paciente.module.css';

/**
 * UC09 — coletas do paciente, da mais recente para a mais antiga, com a versão do
 * modelo de cada análise e o caminho para o resultado.
 */
export function SecaoSessoes({ sessoes }: { sessoes: SessaoDoPaciente[] }) {
  return (
    <section className={estilos.secao} aria-labelledby="titulo-sessoes">
      <h2 id="titulo-sessoes">Coletas</h2>
      {sessoes.length === 0 ? (
        <p className={estilos.nota}>Nenhuma coleta ainda.</p>
      ) : (
        <div className={estilos.rolagem} role="region" aria-label="Tabela de coletas" tabIndex={0}>
          <table className={estilos.tabela}>
            <thead>
              <tr>
                <th scope="col">Iniciada em</th>
                <th scope="col">Coleta</th>
                <th scope="col">Análise</th>
                <th scope="col">Modelo</th>
                <th scope="col">
                  <span className="somente-leitor-de-tela">Abrir</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {sessoes.map((s) => (
                <tr key={s.id}>
                  <th scope="row">{dataHora(s.iniciadaEm)}</th>
                  <td>{NOME_STATUS_SESSAO[s.status]}</td>
                  <td>
                    {s.analiseStatus ? NOME_STATUS_ANALISE[s.analiseStatus] : '—'}
                    {s.analiseConcluidaEm && <span className={estilos.versao}>em {dataHora(s.analiseConcluidaEm)}</span>}
                  </td>
                  <td>
                    {s.versaoModelo ?? '—'}
                    {s.origemDadosModelo === 'SINTETICO' && (
                      <span className={estilos.versao}>treinado com dados sintéticos</span>
                    )}
                  </td>
                  <td className={estilos.acao}>
                    <LinkDaSessao sessao={s} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

/** Um destino por linha; o nome acessível leva a data para diferenciar as linhas. */
function LinkDaSessao({ sessao: s }: { sessao: SessaoDoPaciente }) {
  const quando = dataHora(s.iniciadaEm);
  if (s.analiseId) {
    return (
      <Link to={`/analises/${s.analiseId}`} aria-label={`Ver o resultado da coleta de ${quando}`}>
        Ver o resultado
      </Link>
    );
  }

  const texto = s.status === 'EM_ANDAMENTO' ? 'Continuar coleta' : 'Ver a coleta';
  return (
    <Link to={`/sessoes/${s.id}`} aria-label={`${texto} de ${quando}`}>
      {texto}
    </Link>
  );
}
