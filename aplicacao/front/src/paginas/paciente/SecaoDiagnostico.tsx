import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type SubmitEvent } from 'react';
import { ErroApi, mensagemDeErro } from '../../api/erros';
import { atualizarSituacao, chavesPacientes, type Diagnostico, type NovaSituacao, type Situacao } from '../../api/pacientes';
import { Botao } from '../../componentes/Botao';
import { Campo, CampoTextoLongo } from '../../componentes/Campo';
import { data, dataHora } from '../../componentes/formatos';
import { Mensagem } from '../../componentes/Mensagem';
import { Marcacao, Opcoes } from '../../componentes/Opcoes';
import { hojeIso, NOME_SITUACAO } from '../../dominio/pacientes';
import { useSessao } from '../../sessao/contextoSessao';
import estilos from './Paciente.module.css';

interface PropsSecao {
  pacienteId: string;
  situacao: Situacao;
  diagnostico: Diagnostico | null;
  dataNascimento: string | null;
  /** Só o médico responsável registra a situação (o administrador só vê). */
  podeAtualizar?: boolean;
}

/**
 * UC13 — situação definida pelo diagnóstico clínico, feito fora do sistema.
 * É o padrão de referência de qualquer avaliação futura do sistema (ADR-0009).
 */
export function SecaoDiagnostico({ pacienteId, situacao, diagnostico, dataNascimento, podeAtualizar = true }: PropsSecao) {
  const [editando, setEditando] = useState(false);
  const [aviso, setAviso] = useState<string | null>(null);
  const refSecao = useRef<HTMLElement>(null);

  // Ao abrir o formulário, o foco (e a rolagem) vai para a primeira opção.
  useEffect(() => {
    if (editando) {
      refSecao.current?.querySelector<HTMLInputElement>('form input')?.focus();
    }
  }, [editando]);

  return (
    <section ref={refSecao} className={estilos.secao} aria-labelledby="titulo-diagnostico">
      <div className={estilos.cabecalhoSecao}>
        <h2 id="titulo-diagnostico">Situação diagnóstica</h2>
        {podeAtualizar && !editando && (
          <Botao
            variante="secundario"
            onClick={() => {
              setAviso(null);
              setEditando(true);
            }}
          >
            Atualizar situação diagnóstica
          </Botao>
        )}
      </div>

      {aviso && <Mensagem>{aviso}</Mensagem>}

      {diagnostico ? (
        <dl className={estilos.dados}>
          <div>
            <dt>Situação</dt>
            <dd>{NOME_SITUACAO[situacao]}</dd>
          </div>
          <div>
            <dt>Fonte do diagnóstico</dt>
            <dd>{diagnostico.fonte}</dd>
          </div>
          <div>
            <dt>Avaliação clínica em</dt>
            <dd>{data(diagnostico.avaliadoEm)}</dd>
          </div>
          <div>
            <dt>Registrada no sistema em</dt>
            <dd>{dataHora(diagnostico.atualizadoEm)}</dd>
          </div>
        </dl>
      ) : (
        !editando && (
          <p className={estilos.nota}>
            {podeAtualizar
              ? 'Suspeita de TEA. Quando o diagnóstico clínico for fechado, registre aqui o resultado e como ele foi obtido.'
              : 'Suspeita de TEA. O médico responsável registra o resultado quando o diagnóstico clínico for fechado.'}
          </p>
        )
      )}

      {editando && (
        <FormularioSituacao
          pacienteId={pacienteId}
          situacaoAtual={situacao}
          diagnostico={diagnostico}
          dataNascimento={dataNascimento}
          concluir={(texto) => {
            setEditando(false);
            setAviso(texto);
          }}
          cancelar={() => {
            setEditando(false);
          }}
        />
      )}
    </section>
  );
}

interface Formulario {
  situacao: Situacao | '';
  fonte: string;
  avaliadoEm: string;
  independente: boolean;
}

type Erros = Partial<Record<'situacao' | 'fonte' | 'avaliadoEm' | 'independente', string>>;

const DEFINIDAS: readonly { valor: Situacao; rotulo: string; descricao: string }[] = [
  {
    valor: 'TEA_CONFIRMADO',
    rotulo: NOME_SITUACAO.TEA_CONFIRMADO,
    descricao: 'A avaliação clínica confirmou o transtorno do espectro autista.',
  },
  {
    valor: 'TEA_DESCARTADO',
    rotulo: NOME_SITUACAO.TEA_DESCARTADO,
    descricao: 'A avaliação clínica concluiu que não é TEA.',
  },
  {
    valor: 'CONTROLE',
    rotulo: NOME_SITUACAO.CONTROLE,
    descricao: 'Participante do grupo controle do protocolo, sem suspeita de TEA.',
  },
];

const VOLTAR_PARA_SUSPEITA = {
  valor: 'SUSPEITA' as const,
  rotulo: 'Voltar para suspeita de TEA',
  descricao: 'Só para corrigir um registro feito por engano. Apaga a fonte e a data.',
};

interface PropsFormulario {
  pacienteId: string;
  situacaoAtual: Situacao;
  diagnostico: Diagnostico | null;
  dataNascimento: string | null;
  concluir: (texto: string) => void;
  cancelar: () => void;
}

function FormularioSituacao({ pacienteId, situacaoAtual, diagnostico, dataNascimento, concluir, cancelar }: PropsFormulario) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  // Ao corrigir, os valores atuais vêm preenchidos; a declaração precisa ser feita de novo.
  const [f, setF] = useState<Formulario>({
    situacao: situacaoAtual === 'SUSPEITA' ? '' : situacaoAtual,
    fonte: diagnostico?.fonte ?? '',
    avaliadoEm: diagnostico?.avaliadoEm ?? '',
    independente: false,
  });
  const [erros, setErros] = useState<Erros>({});
  const hoje = hojeIso();

  const atualizacao = useMutation({
    mutationFn: (dados: NovaSituacao) => atualizarSituacao(api, pacienteId, dados),
    onSuccess: (r) => {
      void queryClient.invalidateQueries({ queryKey: chavesPacientes.todas });
      concluir(`Situação diagnóstica atualizada: ${NOME_SITUACAO[r.situacao]}.`);
    },
  });

  // Corrigir um campo apaga o erro dele; os demais ficam até o próximo envio.
  function alterar<K extends keyof Formulario>(campo: K, valor: Formulario[K]) {
    setF((atual) => ({ ...atual, [campo]: valor }));
    setErros((atuais) => Object.fromEntries(Object.entries(atuais).filter(([chave]) => chave !== campo)));
  }

  const voltando = f.situacao === 'SUSPEITA';

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    const novos: Erros = {};
    if (!f.situacao) novos.situacao = 'Escolha a situação.';
    if (f.situacao && !voltando) {
      if (!f.fonte.trim()) novos.fonte = 'Informe como o diagnóstico foi obtido.';
      if (!f.avaliadoEm) novos.avaliadoEm = 'Informe a data da avaliação clínica.';
      else if (f.avaliadoEm > hoje) novos.avaliadoEm = 'A data da avaliação não pode estar no futuro.';
      else if (dataNascimento && f.avaliadoEm < dataNascimento) novos.avaliadoEm = 'A data da avaliação é anterior ao nascimento.';
      if (!f.independente) novos.independente = 'Confirme que o diagnóstico não considerou o resultado do sistema.';
    }
    setErros(novos);
    if (!f.situacao || Object.keys(novos).length > 0) return;

    atualizacao.mutate(
      voltando
        ? { situacao: 'SUSPEITA', fonte: null, avaliadoEm: null, independenteDoSistema: false }
        : { situacao: f.situacao, fonte: f.fonte.trim(), avaliadoEm: f.avaliadoEm, independenteDoSistema: true },
    );
  }

  const erroApi = atualizacao.error instanceof ErroApi ? atualizacao.error : undefined;
  const opcoes = situacaoAtual === 'SUSPEITA' ? DEFINIDAS : [...DEFINIDAS, VOLTAR_PARA_SUSPEITA];

  return (
    <form onSubmit={enviar} noValidate className={estilos.formulario} aria-label="Atualizar situação diagnóstica">
      {atualizacao.error && <Mensagem tipo="erro">{mensagemDeErro(atualizacao.error)}</Mensagem>}

      <p className={estilos.nota}>
        Registre o resultado do diagnóstico clínico, feito fora deste sistema. Essa situação será usada para avaliar, no
        futuro, se os indicadores têm relação com o diagnóstico.
      </p>

      <Opcoes<Situacao>
        legenda="Situação"
        opcoes={opcoes}
        valor={f.situacao}
        aoMudar={(valor) => {
          alterar('situacao', valor);
        }}
        erro={erros.situacao ?? erroApi?.doCampo('Situacao')}
      />

      {f.situacao && !voltando && (
        <>
          <div className={estilos.linha}>
            <CampoTextoLongo
              rotulo="Fonte do diagnóstico"
              rows={2}
              dica="Como a situação foi definida, por exemplo: avaliação multiprofissional, DSM-5-TR. Não escreva nomes de pessoas."
              maxLength={200}
              value={f.fonte}
              onChange={(e) => {
                alterar('fonte', e.target.value);
              }}
              erro={erros.fonte ?? erroApi?.doCampo('Fonte')}
            />
            <Campo
              rotulo="Data da avaliação clínica"
              dica="Dia em que o diagnóstico foi fechado."
              type="date"
              max={hoje}
              min={dataNascimento ?? undefined}
              value={f.avaliadoEm}
              onChange={(e) => {
                alterar('avaliadoEm', e.target.value);
              }}
              erro={erros.avaliadoEm ?? erroApi?.doCampo('AvaliadoEm')}
            />
          </div>

          <Marcacao
            rotulo="O diagnóstico foi fechado sem considerar o resultado deste sistema"
            descricao="Se o resultado da análise influenciou o diagnóstico, a situação não serve como referência para avaliar o sistema e não deve ser registrada aqui."
            marcado={f.independente}
            aoMudar={(v) => {
              alterar('independente', v);
            }}
            erro={erros.independente}
          />
        </>
      )}

      <div className={estilos.acoes}>
        <Botao type="submit" carregando={atualizacao.isPending} textoCarregando="Salvando…">
          Salvar situação
        </Botao>
        <Botao variante="secundario" onClick={cancelar}>
          Cancelar
        </Botao>
      </div>
    </form>
  );
}
