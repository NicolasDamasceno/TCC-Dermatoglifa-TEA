import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useId, useRef, useState, type SubmitEvent } from 'react';
import { ErroApi, mensagemDeErro } from '../api/erros';
import {
  aprovarCadastro,
  CHAVE_PENDENTES,
  listarPendentes,
  recusarCadastro,
  type SolicitacaoPendente,
} from '../api/medicos';
import { Botao } from '../componentes/Botao';
import { CampoTextoLongo } from '../componentes/Campo';
import { dataHora } from '../componentes/formatos';
import { Mensagem } from '../componentes/Mensagem';
import { useTituloPagina } from '../componentes/useTituloPagina';
import { useSessao } from '../sessao/contextoSessao';
import estilos from './CadastrosPendentes.module.css';

type Decisao = 'aprovar' | 'recusar';

interface Resultado {
  nome: string;
  decisao: Decisao;
}

/** UC03 — a administração aprova (com o vínculo ao protocolo) ou recusa (com o motivo). */
export function CadastrosPendentes() {
  useTituloPagina('Cadastros pendentes');
  const { api } = useSessao();
  const pendentes = useQuery({ queryKey: CHAVE_PENDENTES, queryFn: () => listarPendentes(api) });
  const [emAnalise, setEmAnalise] = useState<string | null>(null);
  const [resultado, setResultado] = useState<Resultado | null>(null);
  const refResultado = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (resultado) refResultado.current?.focus();
  }, [resultado]);

  function concluir(solicitacao: SolicitacaoPendente, decisao: Decisao) {
    setEmAnalise(null);
    setResultado({ nome: solicitacao.nome, decisao });
  }

  return (
    <section className={estilos.pagina} aria-labelledby="titulo-pendentes">
      <h1 id="titulo-pendentes">Cadastros pendentes</h1>
      <p className={estilos.introducao}>
        Antes de aprovar, confira o CRM no portal do Conselho Federal de Medicina e se o médico faz parte da equipe do
        protocolo aprovado pelo CEP. Quem não faz parte da equipe deve ser recusado.
      </p>

      {resultado && (
        <div ref={refResultado} tabIndex={-1} className={estilos.resultado}>
          <Mensagem>
            Cadastro de {resultado.nome} {resultado.decisao === 'aprovar' ? 'aprovado' : 'recusado'}.
          </Mensagem>
        </div>
      )}

      {pendentes.isPending && <p className={estilos.carregando}>Carregando pedidos…</p>}

      {pendentes.isError && (
        <Mensagem tipo="erro">
          <p>{mensagemDeErro(pendentes.error)}</p>
          <Botao variante="secundario" className={estilos.tentar} onClick={() => void pendentes.refetch()}>
            Tentar de novo
          </Botao>
        </Mensagem>
      )}

      {pendentes.isSuccess && pendentes.data.length === 0 && (
        <p className={estilos.vazio}>Nenhum pedido aguardando análise.</p>
      )}

      {pendentes.isSuccess && pendentes.data.length > 0 && (
        <div className={estilos.rolagem} role="region" aria-labelledby="legenda-pendentes" tabIndex={0}>
          <table className={estilos.tabela}>
            <caption id="legenda-pendentes" className={estilos.legenda}>
              {pendentes.data.length === 1 ? '1 pedido' : `${pendentes.data.length} pedidos`} aguardando análise, do
              mais antigo ao mais recente
            </caption>
            <thead>
              <tr>
                <th scope="col">Nome</th>
                <th scope="col">CRM</th>
                <th scope="col">Especialidade</th>
                <th scope="col">Pedido em</th>
                <th scope="col">
                  <span className="somente-leitor-de-tela">Ação</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {pendentes.data.map((s) => (
                <LinhaPedido
                  key={s.id}
                  solicitacao={s}
                  aberta={emAnalise === s.id}
                  alternar={() => {
                    setResultado(null);
                    setEmAnalise((atual) => (atual === s.id ? null : s.id));
                  }}
                  concluir={(decisao) => {
                    concluir(s, decisao);
                  }}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

interface PropsLinha {
  solicitacao: SolicitacaoPendente;
  aberta: boolean;
  alternar: () => void;
  concluir: (decisao: Decisao) => void;
}

function LinhaPedido({ solicitacao: s, aberta, alternar, concluir }: PropsLinha) {
  const idPainel = useId();
  return (
    <>
      <tr className={aberta ? estilos.linhaAberta : undefined}>
        <th scope="row">
          <span className={estilos.nome}>{s.nome}</span>
          <span className={estilos.email}>{s.email}</span>
        </th>
        <td className={estilos.numero}>
          {s.crm}/{s.ufCrm}
        </td>
        <td>{s.especialidade}</td>
        <td className={estilos.numero}>{dataHora(s.solicitadoEm)}</td>
        <td className={estilos.acao}>
          <Botao
            variante="secundario"
            aria-expanded={aberta}
            aria-controls={aberta ? idPainel : undefined}
            aria-label={`${aberta ? 'Fechar análise de' : 'Analisar pedido de'} ${s.nome}`}
            onClick={alternar}
          >
            {aberta ? 'Fechar' : 'Analisar'}
          </Botao>
        </td>
      </tr>
      {aberta && (
        <tr className={estilos.painelLinha}>
          <td colSpan={5} id={idPainel}>
            <PainelDecisao solicitacao={s} concluir={concluir} />
          </td>
        </tr>
      )}
    </>
  );
}

function PainelDecisao({ solicitacao, concluir }: { solicitacao: SolicitacaoPendente; concluir: (d: Decisao) => void }) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  const [decisao, setDecisao] = useState<Decisao>('aprovar');
  const [texto, setTexto] = useState('');
  const [erroLocal, setErroLocal] = useState<string>();
  const nomeGrupo = useId();

  const envio = useMutation({
    mutationFn: () =>
      decisao === 'aprovar'
        ? aprovarCadastro(api, solicitacao.id, texto.trim())
        : recusarCadastro(api, solicitacao.id, texto.trim()),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: CHAVE_PENDENTES });
      concluir(decisao);
    },
    onError: (erro) => {
      // Outro administrador pode ter decidido antes: a lista é recarregada.
      if (erro instanceof ErroApi && [404, 409, 422].includes(erro.status)) {
        void queryClient.invalidateQueries({ queryKey: CHAVE_PENDENTES });
      }
    },
  });

  const aprovar = decisao === 'aprovar';

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    if (!texto.trim()) {
      setErroLocal(aprovar ? 'Informe o vínculo com o protocolo de pesquisa.' : 'Informe o motivo da recusa.');
      return;
    }

    setErroLocal(undefined);
    envio.mutate();
  }

  const erroApi = envio.error instanceof ErroApi ? envio.error : undefined;
  const erroCampo =
    erroLocal ?? (aprovar ? erroApi?.doCampo('VinculoProtocolo') : erroApi?.doCampo('Motivo'));

  return (
    <form onSubmit={enviar} noValidate className={estilos.painel} aria-label={`Decisão sobre ${solicitacao.nome}`}>
      {envio.error && !erroCampo && <Mensagem tipo="erro">{mensagemDeErro(envio.error)}</Mensagem>}

      <fieldset className={estilos.decisao}>
        <legend>Decisão</legend>
        {(['aprovar', 'recusar'] as const).map((opcao) => (
          <label key={opcao} className={estilos.opcao}>
            <input
              type="radio"
              name={nomeGrupo}
              value={opcao}
              checked={decisao === opcao}
              onChange={() => {
                setDecisao(opcao);
                setTexto('');
                setErroLocal(undefined);
                envio.reset();
              }}
            />
            {opcao === 'aprovar' ? 'Aprovar' : 'Recusar'}
          </label>
        ))}
      </fieldset>

      <CampoTextoLongo
        key={decisao}
        rotulo={aprovar ? 'Vínculo com o protocolo' : 'Motivo da recusa'}
        dica={
          aprovar
            ? 'Identifique o protocolo aprovado pelo CEP e a função do médico na equipe.'
            : 'Fica registrado no cadastro do médico.'
        }
        maxLength={aprovar ? 200 : 300}
        value={texto}
        onChange={(e) => {
          setTexto(e.target.value);
        }}
        erro={erroCampo}
      />

      <Botao type="submit" carregando={envio.isPending} textoCarregando="Registrando…">
        {aprovar ? 'Aprovar cadastro' : 'Recusar cadastro'}
      </Botao>
    </form>
  );
}
