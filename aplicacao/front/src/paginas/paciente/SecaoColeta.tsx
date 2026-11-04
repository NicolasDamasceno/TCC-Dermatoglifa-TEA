import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type SubmitEvent } from 'react';
import { useNavigate } from 'react-router';
import { ErroApi, mensagemDeErro } from '../../api/erros';
import {
  chavesPacientes,
  iniciarSessao,
  type MetodoCaptura,
  type NovaSessao,
  type SessaoDoPaciente,
  type TipoImpressao,
} from '../../api/pacientes';
import { Botao } from '../../componentes/Botao';
import { Campo } from '../../componentes/Campo';
import { dataHora } from '../../componentes/formatos';
import { LinkBotao } from '../../componentes/LinkBotao';
import { Mensagem } from '../../componentes/Mensagem';
import { Opcoes } from '../../componentes/Opcoes';
import { NOME_IMPRESSAO, NOME_METODO } from '../../dominio/pacientes';
import { useSessao } from '../../sessao/contextoSessao';
import estilos from './Paciente.module.css';

interface PropsSecaoColeta {
  pacienteId: string;
  podeColetar: boolean;
  anonimizado: boolean;
  sessoes: SessaoDoPaciente[];
  registrarConsentimento: () => void;
}

/** O que o médico pode fazer agora: continuar, iniciar ou por que ainda não pode coletar. */
export function SecaoColeta({ pacienteId, podeColetar, anonimizado, sessoes, registrarConsentimento }: PropsSecaoColeta) {
  const [iniciando, setIniciando] = useState(false);
  const emAndamento = sessoes.find((s) => s.status === 'EM_ANDAMENTO');

  let conteudo;
  if (anonimizado) {
    conteudo = <p>Os dados deste paciente foram eliminados a pedido do titular. Não há nova coleta.</p>;
  } else if (emAndamento && !podeColetar) {
    conteudo = (
      <>
        <p>
          Há uma coleta iniciada em {dataHora(emAndamento.iniciadaEm)}, mas não há mais TCLE vigente que libere a
          coleta: ela não pode continuar. Registre um novo TCLE para retomá-la, ou cancele-a na tela da coleta.
        </p>
        <div className={estilos.acoes}>
          <Botao onClick={registrarConsentimento}>Registrar o TCLE</Botao>
          <LinkBotao to={`/sessoes/${emAndamento.id}`} variante="secundario">
            Ver a coleta
          </LinkBotao>
        </div>
      </>
    );
  } else if (emAndamento) {
    conteudo = (
      <>
        <p>Há uma coleta em andamento, iniciada em {dataHora(emAndamento.iniciadaEm)}.</p>
        <LinkBotao to={`/sessoes/${emAndamento.id}`}>Continuar coleta</LinkBotao>
      </>
    );
  } else if (podeColetar) {
    conteudo = iniciando ? (
      <FormularioInicio
        pacienteId={pacienteId}
        cancelar={() => {
          setIniciando(false);
        }}
      />
    ) : (
      <>
        <p>Coleta liberada por um TCLE vigente.</p>
        <Botao
          onClick={() => {
            setIniciando(true);
          }}
        >
          Iniciar coleta
        </Botao>
      </>
    );
  } else {
    conteudo = (
      <>
        <p>
          A coleta ainda não está liberada. Ela depende de um TCLE vigente que autorize a análise das digitais e registre
          a ciência do uso de IA.
        </p>
        <Botao variante="secundario" onClick={registrarConsentimento}>
          Registrar o TCLE
        </Botao>
      </>
    );
  }

  return (
    <section className={`${estilos.secao} ${estilos.coleta}`} aria-labelledby="titulo-coleta">
      <h2 id="titulo-coleta">Coleta das digitais</h2>
      {conteudo}
    </section>
  );
}

interface ErrosInicio {
  metodo?: string;
  tipoImpressao?: string;
}

function FormularioInicio({ pacienteId, cancelar }: { pacienteId: string; cancelar: () => void }) {
  const { api } = useSessao();
  const navegar = useNavigate();
  const queryClient = useQueryClient();
  const [metodo, setMetodo] = useState<MetodoCaptura | ''>('');
  const [tipoImpressao, setTipoImpressao] = useState<TipoImpressao | ''>('');
  const [dispositivo, setDispositivo] = useState('');
  const [erros, setErros] = useState<ErrosInicio>({});

  const inicio = useMutation({
    mutationFn: (dados: NovaSessao) => iniciarSessao(api, pacienteId, dados),
    onSuccess: ({ id }) => {
      void queryClient.invalidateQueries({ queryKey: chavesPacientes.detalhe(pacienteId) });
      void navegar(`/sessoes/${id}`);
    },
    onError: (erro) => {
      // 409: já existe sessão em andamento; 422: consentimento mudou. A tela se atualiza.
      if (erro instanceof ErroApi && (erro.status === 409 || erro.status === 422)) {
        void queryClient.invalidateQueries({ queryKey: chavesPacientes.detalhe(pacienteId) });
      }
    },
  });

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    const novos: ErrosInicio = {};
    if (!metodo) novos.metodo = 'Escolha o método de captura.';
    if (!tipoImpressao) novos.tipoImpressao = 'Escolha o tipo de impressão.';
    setErros(novos);
    if (!metodo || !tipoImpressao) return;
    inicio.mutate({ metodo, tipoImpressao, dispositivo: dispositivo.trim() || null });
  }

  return (
    <form onSubmit={enviar} noValidate className={estilos.formulario} aria-label="Iniciar coleta">
      {inicio.error && <Mensagem tipo="erro">{mensagemDeErro(inicio.error)}</Mensagem>}
      <Opcoes<MetodoCaptura>
        legenda="Método de captura"
        opcoes={[
          { valor: 'LEITOR_OPTICO', rotulo: NOME_METODO.LEITOR_OPTICO },
          { valor: 'TINTA_DIGITALIZADA', rotulo: NOME_METODO.TINTA_DIGITALIZADA },
        ]}
        valor={metodo}
        aoMudar={setMetodo}
        erro={erros.metodo}
        emLinha
      />
      <Opcoes<TipoImpressao>
        legenda="Tipo de impressão"
        opcoes={[
          { valor: 'ROLADA', rotulo: NOME_IMPRESSAO.ROLADA, descricao: 'Dedo rolado de uma borda à outra da unha.' },
          { valor: 'PLANA', rotulo: NOME_IMPRESSAO.PLANA, descricao: 'Dedo apenas pousado, sem rolar.' },
        ]}
        valor={tipoImpressao}
        aoMudar={setTipoImpressao}
        erro={erros.tipoImpressao}
      />
      <Campo
        rotulo="Dispositivo (opcional)"
        dica="Modelo do leitor ou do scanner, para comparar coletas depois."
        maxLength={100}
        value={dispositivo}
        onChange={(e) => {
          setDispositivo(e.target.value);
        }}
      />
      <div className={estilos.acoes}>
        <Botao type="submit" carregando={inicio.isPending} textoCarregando="Iniciando…">
          Iniciar coleta
        </Botao>
        <Botao variante="secundario" onClick={cancelar}>
          Cancelar
        </Botao>
      </div>
    </form>
  );
}
