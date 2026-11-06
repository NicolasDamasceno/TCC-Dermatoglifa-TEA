import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type SubmitEvent } from 'react';
import { chavesAnalises, confirmarRegistroProntuario, salvarObservacao, type Analise } from '../../api/analises';
import { ErroApi, mensagemDeErro } from '../../api/erros';
import { Botao } from '../../componentes/Botao';
import { CampoTextoLongo } from '../../componentes/Campo';
import { dataHora } from '../../componentes/formatos';
import { Mensagem } from '../../componentes/Mensagem';
import { Marcacao } from '../../componentes/Opcoes';
import { useSessao } from '../../sessao/contextoSessao';
import estilos from './Resultado.module.css';

/**
 * UC08, passos 3 e 4: observação do médico e confirmação do registro no prontuário.
 * Para o administrador, só leitura (ADR-0010).
 */
export function Registro({ analise: a, somenteLeitura = false }: { analise: Analise; somenteLeitura?: boolean }) {
  return (
    <section className={estilos.secao} aria-labelledby="titulo-registro">
      <h2 id="titulo-registro">Registro do médico</h2>
      {somenteLeitura ? (
        <dl className={estilos.registroLeitura}>
          <div>
            <dt>Observação do médico</dt>
            <dd>{a.observacaoMedico ?? 'Nenhuma.'}</dd>
          </div>
          <div>
            <dt>Registro no prontuário</dt>
            <dd>{a.registradoProntuarioEm ? `Confirmado em ${dataHora(a.registradoProntuarioEm)}` : 'Ainda não confirmado.'}</dd>
          </div>
        </dl>
      ) : (
        <>
          <Observacao analise={a} />
          <Prontuario analise={a} />
        </>
      )}
    </section>
  );
}

function Observacao({ analise: a }: { analise: Analise }) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  const [texto, setTexto] = useState(a.observacaoMedico ?? '');
  const [salva, setSalva] = useState(false);
  const alterada = texto.trim() !== (a.observacaoMedico ?? '');

  const envio = useMutation({
    mutationFn: () => salvarObservacao(api, a.id, texto.trim() || null),
    onSuccess: (nova) => {
      queryClient.setQueryData(chavesAnalises.analise(a.id), nova);
      setTexto(nova.observacaoMedico ?? '');
      setSalva(true);
    },
  });

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    envio.mutate();
  }

  const erroApi = envio.error instanceof ErroApi ? envio.error : undefined;

  return (
    <form onSubmit={enviar} noValidate className={estilos.formulario}>
      <CampoTextoLongo
        rotulo="Observação do médico"
        dica="Opcional. Por exemplo, o contexto clínico em que este resultado foi considerado."
        maxLength={2000}
        rows={4}
        value={texto}
        onChange={(e) => {
          setTexto(e.target.value);
          setSalva(false);
        }}
        erro={erroApi?.doCampo('Observacao')}
      />
      {envio.error && !erroApi?.doCampo('Observacao') && <Mensagem tipo="erro">{mensagemDeErro(envio.error)}</Mensagem>}
      <div className={estilos.acoes}>
        <Botao type="submit" variante="secundario" disabled={!alterada} carregando={envio.isPending} textoCarregando="Salvando…">
          Salvar observação
        </Botao>
        {salva && !alterada && (
          <span role="status" className={estilos.salva}>
            Observação salva.
          </span>
        )}
      </div>
    </form>
  );
}

function Prontuario({ analise: a }: { analise: Analise }) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  const [registrei, setRegistrei] = useState(false);

  const confirmacao = useMutation({
    mutationFn: () => confirmarRegistroProntuario(api, a.id),
    onSuccess: (nova) => {
      queryClient.setQueryData(chavesAnalises.analise(a.id), nova);
    },
  });

  if (a.registradoProntuarioEm) {
    return (
      <div className={estilos.prontuario}>
        <h3>Prontuário</h3>
        <p className={estilos.confirmado}>Registro no prontuário confirmado em {dataHora(a.registradoProntuarioEm)}.</p>
      </div>
    );
  }

  return (
    <div className={estilos.prontuario} role="group" aria-labelledby="titulo-prontuario">
      <h3 id="titulo-prontuario">Prontuário</h3>
      <p>
        A Res. CFM 2.454/2026 pede que o uso de IA como apoio fique registrado no prontuário do paciente. Depois de
        registrar, confirme aqui; a data fica guardada com a análise.
      </p>
      {confirmacao.error && <Mensagem tipo="erro">{mensagemDeErro(confirmacao.error)}</Mensagem>}
      <Marcacao
        rotulo="Registrei no prontuário que esta análise de IA foi usada como apoio à minha decisão"
        marcado={registrei}
        aoMudar={setRegistrei}
      />
      <Botao
        disabled={!registrei}
        carregando={confirmacao.isPending}
        textoCarregando="Confirmando…"
        onClick={() => {
          confirmacao.mutate();
        }}
      >
        Confirmar registro no prontuário
      </Botao>
    </div>
  );
}
