import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router';
import { chavesAnalises, novaTentativa, obterAnalise, type Analise } from '../../api/analises';
import { ErroApi, mensagemDeErro } from '../../api/erros';
import { chavesPacientes, obterPaciente } from '../../api/pacientes';
import { AvisoNaoDiagnostico } from '../../componentes/AvisoNaoDiagnostico';
import { Botao } from '../../componentes/Botao';
import { Carregando, FalhaAoCarregar } from '../../componentes/Consulta';
import { dataHora } from '../../componentes/formatos';
import { Mensagem } from '../../componentes/Mensagem';
import { useTituloPagina } from '../../componentes/useTituloPagina';
import { NOME_STATUS_ANALISE } from '../../dominio/pacientes';
import { useSessao } from '../../sessao/contextoSessao';
import { FichaAnotada } from './FichaAnotada';
import { Indicadores } from './Indicadores';
import { Modelo } from './Modelo';
import { Registro } from './Registro';
import estilos from './Resultado.module.css';

const EM_ANDAMENTO = new Set(['PENDENTE', 'PROCESSANDO']);

/** UC08 — o resultado, sempre na mesma ordem de leitura (doc 09, seção 4). */
export function Resultado() {
  useTituloPagina('Resultado da análise');
  const { id = '' } = useParams();
  const { api } = useSessao();
  const analise = useQuery({
    queryKey: chavesAnalises.analise(id),
    queryFn: () => obterAnalise(api, id),
    // Enquanto a IA processa, a tela confere de novo sozinha.
    refetchInterval: (q) => (q.state.data && EM_ANDAMENTO.has(q.state.data.status) ? 4000 : false),
  });

  if (analise.isPending) return <Carregando texto="Carregando o resultado…" />;

  if (analise.isError) {
    if (analise.error instanceof ErroApi && analise.error.status === 404) {
      return (
        <section className={estilos.pagina} aria-labelledby="titulo-resultado">
          <h1 id="titulo-resultado">Resultado não encontrado</h1>
          <p>Ele não existe ou é de um paciente de outro médico.</p>
          <p>
            <Link to="/pacientes">Voltar para Pacientes</Link>
          </p>
        </section>
      );
    }

    return <FalhaAoCarregar erro={analise.error} tentar={() => void analise.refetch()} />;
  }

  return <TelaResultado analise={analise.data} />;
}

function TelaResultado({ analise: a }: { analise: Analise }) {
  const { api, sessao } = useSessao();
  // ADR-0010: o administrador lê o resultado; agir sobre ele é do médico.
  const admin = sessao?.usuario.papel === 'ADMIN';
  const paciente = useQuery({
    queryKey: chavesPacientes.detalhe(a.pacienteId),
    queryFn: () => obterPaciente(api, a.pacienteId),
  });
  const nome = paciente.data?.paciente.nome;
  const escoreVisivel = a.status === 'CONCLUIDA' && a.modelo?.escoreValidado === true && a.escoreRisco !== null;

  return (
    <section className={estilos.pagina} aria-labelledby="titulo-resultado">
      <header className={estilos.cabecalho}>
        <p className={estilos.voltar}>
          <Link to={`/pacientes/${a.pacienteId}`}>{nome ?? 'Paciente'}</Link>
        </p>
        <h1 id="titulo-resultado">Resultado da análise</h1>
        <dl className={estilos.resumo}>
          <div>
            <dt>Paciente</dt>
            <dd>{nome ?? '…'}</dd>
          </div>
          <div>
            <dt>Situação</dt>
            <dd>{NOME_STATUS_ANALISE[a.status]}</dd>
          </div>
          <div>
            <dt>Solicitada em</dt>
            <dd>{dataHora(a.solicitadaEm)}</dd>
          </div>
          {a.concluidaEm && (
            <div>
              <dt>Concluída em</dt>
              <dd>{dataHora(a.concluidaEm)}</dd>
            </div>
          )}
        </dl>
        {!admin && (
          <p className={estilos.voltar}>
            <Link to={`/sessoes/${a.sessaoId}`}>Ver a coleta</Link>
          </p>
        )}
      </header>

      <AvisoNaoDiagnostico texto={a.aviso.texto} versao={a.aviso.versao} />

      {a.alertas.length > 0 && (
        <section className={estilos.alertas} aria-labelledby="titulo-alertas">
          <h2 id="titulo-alertas">Leia também</h2>
          <ul>
            {a.alertas.map((alerta) => (
              <li key={alerta.codigo} className={alerta.codigo === 'MODELO_SINTETICO' ? estilos.alertaForte : undefined}>
                {alerta.texto}
              </li>
            ))}
          </ul>
        </section>
      )}

      {EM_ANDAMENTO.has(a.status) && (
        <Mensagem titulo="Análise em processamento.">
          <p>Esta tela se atualiza sozinha quando o resultado ficar pronto.</p>
        </Mensagem>
      )}

      {a.status === 'ERRO' && <ErroAnalise analise={a} podeTentar={!admin} />}

      {a.status === 'CONCLUIDA' && (
        <>
          <FichaAnotada digitais={a.digitais} />
          {a.indicadores && <Indicadores indicadores={a.indicadores} />}
          {escoreVisivel && (
            <section className={estilos.secao} aria-labelledby="titulo-escore">
              <h2 id="titulo-escore">Escore de risco</h2>
              <p className={estilos.escore}>{a.escoreRisco}</p>
              <p className={estilos.nota}>
                Apoio à decisão do médico, sem ponto de corte validado. Não classifica o paciente.
              </p>
            </section>
          )}
        </>
      )}

      {a.modelo && <Modelo modelo={a.modelo} />}

      {a.status === 'CONCLUIDA' && <Registro analise={a} somenteLeitura={admin} />}
    </section>
  );
}

function ErroAnalise({ analise: a, podeTentar }: { analise: Analise; podeTentar: boolean }) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  const tentativa = useMutation({
    mutationFn: () => novaTentativa(api, a.id),
    onSuccess: (nova) => {
      queryClient.setQueryData(chavesAnalises.analise(a.id), nova);
      void queryClient.invalidateQueries({ queryKey: chavesPacientes.detalhe(a.pacienteId) });
    },
  });

  return (
    <Mensagem tipo="erro" titulo="A análise não foi concluída.">
      <p>{a.mensagemErro ?? 'O serviço de análise não respondeu.'}</p>
      <p>As digitais continuam guardadas; dá para tentar de novo sem refazer a coleta.</p>
      {tentativa.error && <p className={estilos.erroTentativa}>{mensagemDeErro(tentativa.error)}</p>}
      {podeTentar && (
        <Botao
          className={estilos.tentar}
          carregando={tentativa.isPending}
          textoCarregando="Analisando…"
          onClick={() => {
            tentativa.mutate();
          }}
        >
          Tentar análise de novo
        </Botao>
      )}
    </Mensagem>
  );
}
