import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import {
  cancelarSessao,
  chavesColetas,
  concluirSessao,
  obterSessao,
  type DedoSessao,
  type SessaoColeta,
} from '../../api/coletas';
import { ErroApi, mensagemDeErro } from '../../api/erros';
import { chavesPacientes, obterPaciente } from '../../api/pacientes';
import { Botao } from '../../componentes/Botao';
import { Carregando, FalhaAoCarregar } from '../../componentes/Consulta';
import { FichaDecadactilar } from '../../componentes/FichaDecadactilar';
import { dataHora } from '../../componentes/formatos';
import { Mensagem } from '../../componentes/Mensagem';
import { useTituloPagina } from '../../componentes/useTituloPagina';
import { chaveDedo, NOME_DEDO, NOME_MAO, nomeDoDedo, type Dedo, type Mao } from '../../dominio/dedos';
import { NOME_IMPRESSAO, NOME_METODO, NOME_STATUS_SESSAO } from '../../dominio/pacientes';
import { useSessao } from '../../sessao/contextoSessao';
import { PainelDedo } from './PainelDedo';
import { proximoPendente } from './ordem';
import estilos from './Coleta.module.css';

/** UC06 — coleta das 10 digitais na ficha decadactilar. */
export function Coleta() {
  useTituloPagina('Coleta das digitais');
  const { id = '' } = useParams();
  const { api } = useSessao();
  const sessao = useQuery({ queryKey: chavesColetas.sessao(id), queryFn: () => obterSessao(api, id) });

  if (sessao.isPending) return <Carregando texto="Carregando a coleta…" />;

  if (sessao.isError) {
    if (sessao.error instanceof ErroApi && sessao.error.status === 404) {
      return (
        <section className={estilos.pagina} aria-labelledby="titulo-coleta">
          <h1 id="titulo-coleta">Coleta não encontrada</h1>
          <p>Ela não existe ou é de um paciente de outro médico.</p>
          <p>
            <Link to="/pacientes">Voltar para Pacientes</Link>
          </p>
        </section>
      );
    }

    return <FalhaAoCarregar erro={sessao.error} tentar={() => void sessao.refetch()} />;
  }

  return <TelaColeta sessao={sessao.data} />;
}

function TelaColeta({ sessao }: { sessao: SessaoColeta }) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  const navegar = useNavigate();
  const emAndamento = sessao.status === 'EM_ANDAMENTO';

  const paciente = useQuery({
    queryKey: chavesPacientes.detalhe(sessao.pacienteId),
    queryFn: () => obterPaciente(api, sessao.pacienteId),
  });

  // Miniaturas das imagens enviadas nesta visita: existem só na memória da aba (RNF13).
  const [miniaturas, setMiniaturas] = useState<Record<string, string>>({});
  const refMiniaturas = useRef(miniaturas);
  useEffect(() => {
    refMiniaturas.current = miniaturas;
  }, [miniaturas]);
  useEffect(
    () => () => {
      Object.values(refMiniaturas.current).forEach((url) => {
        URL.revokeObjectURL(url);
      });
    },
    [],
  );

  const [selecionado, setSelecionado] = useState<string | undefined>(() => {
    const pendente = proximoPendente(sessao.dedos);
    return pendente ? chaveDedo(pendente.mao, pendente.dedo) : undefined;
  });
  const [anuncio, setAnuncio] = useState('');
  const [focarPainel, setFocarPainel] = useState(false);
  const [confirmandoCancelamento, setConfirmandoCancelamento] = useState(false);

  const atualizar = useCallback(
    (nova: SessaoColeta) => {
      queryClient.setQueryData(chavesColetas.sessao(nova.id), nova);
      void queryClient.invalidateQueries({ queryKey: chavesPacientes.detalhe(nova.pacienteId) });
    },
    [queryClient],
  );

  /** Depois de resolver um dedo, a ficha passa sozinha ao próximo pendente. */
  const aposResolver = useCallback(
    (nova: SessaoColeta, mao: Mao, dedo: Dedo, texto: string, miniatura?: string) => {
      atualizar(nova);
      const chave = chaveDedo(mao, dedo);
      setMiniaturas((atual) => {
        const anterior = atual[chave];
        if (anterior && anterior !== miniatura) URL.revokeObjectURL(anterior);
        const proximas = Object.fromEntries(Object.entries(atual).filter(([k]) => k !== chave));
        if (miniatura) proximas[chave] = miniatura;
        return proximas;
      });
      const proximo = proximoPendente(nova.dedos, chave);
      setSelecionado(proximo ? chaveDedo(proximo.mao, proximo.dedo) : chave);
      setFocarPainel(true);
      setAnuncio(
        proximo
          ? `${texto} Próximo: ${nomeDoDedo(proximo.mao, proximo.dedo)}.`
          : `${texto} Todos os dedos resolvidos: a coleta pode ser concluída.`,
      );
    },
    [atualizar],
  );

  const conclusao = useMutation({
    mutationFn: () => concluirSessao(api, sessao.id),
    onSuccess: (nova) => {
      atualizar(nova);
      if (nova.analiseId) void navegar(`/analises/${nova.analiseId}`);
    },
  });

  const cancelamento = useMutation({
    mutationFn: () => cancelarSessao(api, sessao.id),
    onSuccess: (nova) => {
      setConfirmandoCancelamento(false);
      atualizar(nova);
    },
  });

  const pendentes = sessao.dedos.filter((d) => d.situacao === 'PENDENTE').length;
  const naoColetaveis = sessao.dedos.filter((d) => d.situacao === 'NAO_COLETAVEL').length;
  const resolvidos = sessao.dedos.length - pendentes;
  const dedoSelecionado = sessao.dedos.find((d) => chaveDedo(d.mao, d.dedo) === selecionado);
  const nomePaciente = paciente.data?.paciente.nome;

  return (
    <section className={estilos.pagina} aria-labelledby="titulo-coleta">
      <header className={estilos.cabecalho}>
        <p className={estilos.voltar}>
          <Link to={`/pacientes/${sessao.pacienteId}`}>{nomePaciente ?? 'Paciente'}</Link>
        </p>
        <h1 id="titulo-coleta">Coleta das digitais</h1>
        <dl className={estilos.resumo}>
          <div>
            <dt>Paciente</dt>
            <dd>{nomePaciente ?? '…'}</dd>
          </div>
          <div>
            <dt>Idade na coleta</dt>
            <dd>{sessao.idadeMeses === 1 ? '1 mês' : `${sessao.idadeMeses} meses`}</dd>
          </div>
          <div>
            <dt>Captura</dt>
            <dd>
              {NOME_METODO[sessao.metodo]}, {NOME_IMPRESSAO[sessao.tipoImpressao].toLowerCase()}
            </dd>
          </div>
          {sessao.dispositivo && (
            <div>
              <dt>Dispositivo</dt>
              <dd>{sessao.dispositivo}</dd>
            </div>
          )}
          <div>
            <dt>Iniciada em</dt>
            <dd>{dataHora(sessao.iniciadaEm)}</dd>
          </div>
        </dl>
      </header>

      {!emAndamento && <SituacaoFinal sessao={sessao} naoColetaveis={naoColetaveis} />}

      <div className={estilos.areaFicha}>
        <p className={estilos.progresso} id="progresso-coleta">
          <strong>
            {resolvidos} de {sessao.dedos.length}
          </strong>{' '}
          dedos resolvidos
          {naoColetaveis > 0 && `, ${naoColetaveis === 1 ? '1 não coletável' : `${naoColetaveis} não coletáveis`}`}
        </p>
        <FichaDecadactilar
          legenda="Ficha decadactilar"
          interativa={emAndamento}
          className={estilos.ficha}
          quadro={(mao, dedo) => {
            const d = sessao.dedos.find((x) => x.mao === mao && x.dedo === dedo);
            if (!d) return null;
            const chave = chaveDedo(mao, dedo);
            return (
              <Quadro
                dedo={d}
                miniatura={miniaturas[chave]}
                selecionado={emAndamento && chave === selecionado}
                selecionar={
                  emAndamento
                    ? () => {
                        setSelecionado(chave);
                        setFocarPainel(false);
                        setAnuncio('');
                      }
                    : undefined
                }
              />
            );
          }}
        />
        <p className={estilos.legendaCompacta} aria-hidden="true">
          <span className={estilos.marcaColetado} /> coletado <span className={estilos.marcaNaoColetavel} /> não coletável{' '}
          <span className={estilos.marcaPendente} /> pendente
        </p>
      </div>

      <p className="somente-leitor-de-tela" role="status" aria-live="polite">
        {anuncio}
      </p>

      {emAndamento && dedoSelecionado && (
        <PainelDedo
          key={selecionado}
          sessaoId={sessao.id}
          dedo={dedoSelecionado}
          focarAoAbrir={focarPainel}
          aposResolver={(nova, texto, miniatura) => {
            aposResolver(nova, dedoSelecionado.mao, dedoSelecionado.dedo, texto, miniatura);
          }}
        />
      )}

      {emAndamento && (
        <section className={estilos.encerrar} aria-labelledby="titulo-encerrar">
          <h2 id="titulo-encerrar">Concluir</h2>
          {conclusao.error && <Mensagem tipo="erro">{mensagemDeErro(conclusao.error)}</Mensagem>}
          {pendentes > 0 ? (
            <p>
              Faltam {pendentes === 1 ? '1 dedo' : `${pendentes} dedos`}. Envie a imagem ou registre como não coletável
              para poder concluir.
            </p>
          ) : naoColetaveis > 0 ? (
            <p>
              Há dedo registrado como não coletável: a coleta termina incompleta e, nesta versão, não é analisada.
            </p>
          ) : (
            <p>Os 10 dedos foram coletados. Ao concluir, a análise é solicitada automaticamente.</p>
          )}
          <div className={estilos.acoes}>
            <Botao
              disabled={pendentes > 0}
              aria-describedby="progresso-coleta"
              carregando={conclusao.isPending}
              textoCarregando="Concluindo e analisando…"
              onClick={() => {
                conclusao.mutate();
              }}
            >
              Concluir coleta
            </Botao>
            {!confirmandoCancelamento && (
              <Botao
                variante="secundario"
                onClick={() => {
                  setConfirmandoCancelamento(true);
                }}
              >
                Cancelar coleta
              </Botao>
            )}
          </div>

          {confirmandoCancelamento && (
            <div className={estilos.confirmacao} role="group" aria-label="Confirmar cancelamento da coleta">
              {cancelamento.error && <Mensagem tipo="erro">{mensagemDeErro(cancelamento.error)}</Mensagem>}
              <p>
                Cancelar esta coleta? Ela é encerrada sem análise e não pode ser retomada. Para coletar de novo, inicie
                outra coleta na tela do paciente.
              </p>
              <div className={estilos.acoes}>
                <Botao
                  carregando={cancelamento.isPending}
                  textoCarregando="Cancelando…"
                  onClick={() => {
                    cancelamento.mutate();
                  }}
                >
                  Confirmar cancelamento
                </Botao>
                <Botao
                  variante="secundario"
                  onClick={() => {
                    setConfirmandoCancelamento(false);
                  }}
                >
                  Continuar coletando
                </Botao>
              </div>
            </div>
          )}
        </section>
      )}
    </section>
  );
}

function SituacaoFinal({ sessao, naoColetaveis }: { sessao: SessaoColeta; naoColetaveis: number }) {
  const status = NOME_STATUS_SESSAO[sessao.status];
  const quando = sessao.concluidaEm ? ` em ${dataHora(sessao.concluidaEm)}` : '';
  return (
    <Mensagem titulo={`Coleta ${status.toLowerCase()}${quando}.`}>
      {sessao.status === 'COMPLETA' && sessao.analiseId && (
        <p>
          <Link to={`/analises/${sessao.analiseId}`}>Ver o resultado da análise</Link>
        </p>
      )}
      {sessao.status === 'INCOMPLETA' && (
        <p>
          {naoColetaveis === 1 ? '1 dedo não pôde ser coletado' : `${naoColetaveis} dedos não puderam ser coletados`}.
          Coletas incompletas não são analisadas nesta versão.
        </p>
      )}
      {sessao.status === 'CANCELADA' && <p>Encerrada sem análise.</p>}
      <p>
        <Link to={`/pacientes/${sessao.pacienteId}`}>Voltar para o paciente</Link>
      </p>
    </Mensagem>
  );
}

interface PropsQuadro {
  dedo: DedoSessao;
  miniatura: string | undefined;
  selecionado: boolean;
  selecionar: (() => void) | undefined;
}

const TEXTO_SITUACAO = { PENDENTE: 'Pendente', COLETADO: 'Coletado', NAO_COLETAVEL: 'Não coletável' } as const;

function Quadro({ dedo: d, miniatura, selecionado, selecionar }: PropsQuadro) {
  const classes = [estilos.quadro, estilos[d.situacao], selecionado ? estilos.selecionado : undefined]
    .filter(Boolean)
    .join(' ');
  const conteudo = (
    <>
      {miniatura && d.situacao === 'COLETADO' && <img src={miniatura} alt="" className={estilos.miniatura} />}
      <span className={estilos.estado}>
        <span className={estilos.textoEstado}>{TEXTO_SITUACAO[d.situacao]}</span>
      </span>
    </>
  );

  if (!selecionar) {
    return <div className={classes}>{conteudo}</div>;
  }

  return (
    <button
      type="button"
      className={classes}
      aria-pressed={selecionado}
      aria-label={`${NOME_MAO[d.mao]}, ${NOME_DEDO[d.dedo]}: ${TEXTO_SITUACAO[d.situacao].toLowerCase()}`}
      onClick={selecionar}
    >
      {conteudo}
    </button>
  );
}
