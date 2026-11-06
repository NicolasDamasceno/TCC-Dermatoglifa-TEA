import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type SubmitEvent } from 'react';
import { ErroApi, mensagemDeErro } from '../../api/erros';
import {
  chavesPacientes,
  corrigirIdentificacao,
  eliminarDados,
  exportarDados,
  type NovaIdentificacao,
  type PacienteResumo,
  type Responsavel,
} from '../../api/pacientes';
import { Botao } from '../../componentes/Botao';
import { Campo, CampoSelecao } from '../../componentes/Campo';
import { Mensagem } from '../../componentes/Mensagem';
import { ehMenorDeIdade, hojeIso, PARENTESCOS } from '../../dominio/pacientes';
import { useSessao } from '../../sessao/contextoSessao';
import estilos from './Paciente.module.css';

type Painel = 'corrigir' | 'eliminar' | null;

const CONFIRMACAO = 'ELIMINAR';

/** Entrega o arquivo baixado ao navegador como download. */
function salvarArquivo(nome: string, conteudo: Blob) {
  const url = URL.createObjectURL(conteudo);
  const link = document.createElement('a');
  link.href = url;
  link.download = nome;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

/**
 * UC12 — pedidos do titular (LGPD art. 18), só para o administrador (ADR-0010):
 * exportar, corrigir a identificação e eliminar. A revogação de termo fica na
 * seção de consentimentos.
 */
export function SecaoTitular({ paciente, responsavel }: { paciente: PacienteResumo; responsavel: Responsavel | null }) {
  const { api } = useSessao();
  const [painel, setPainel] = useState<Painel>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  const exportacao = useMutation({
    mutationFn: () => exportarDados(api, paciente.id),
    onSuccess: ({ nome, conteudo }) => {
      salvarArquivo(nome, conteudo);
      setAviso(
        `Arquivo ${nome} baixado. Ele tem dados pessoais e as imagens das digitais: entregue ao titular por um meio seguro e apague a cópia deste computador.`,
      );
    },
  });

  function abrir(qual: Painel) {
    setAviso(null);
    setPainel(qual);
  }

  return (
    <section className={estilos.secao} aria-labelledby="titulo-titular">
      <h2 id="titulo-titular">Pedidos do titular (LGPD)</h2>
      <p className={estilos.nota}>
        Atenda aqui o que o titular ou o responsável pedir. Cada ação fica na trilha de auditoria. Para revogar um termo,
        use a seção Consentimentos.
      </p>

      {aviso && <Mensagem>{aviso}</Mensagem>}
      {exportacao.error && <Mensagem tipo="erro">{mensagemDeErro(exportacao.error)}</Mensagem>}

      {painel === null && (
        <div className={estilos.acoes}>
          <Botao
            variante="secundario"
            carregando={exportacao.isPending}
            textoCarregando="Preparando o arquivo…"
            onClick={() => {
              setAviso(null);
              exportacao.mutate();
            }}
          >
            Exportar dados
          </Botao>
          <Botao
            variante="secundario"
            onClick={() => {
              abrir('corrigir');
            }}
          >
            Corrigir identificação
          </Botao>
          <Botao
            variante="secundario"
            onClick={() => {
              abrir('eliminar');
            }}
          >
            Eliminar dados
          </Botao>
        </div>
      )}

      {painel === 'corrigir' && (
        <FormularioCorrecao
          paciente={paciente}
          responsavel={responsavel}
          concluir={(texto) => {
            setPainel(null);
            setAviso(texto);
          }}
          cancelar={() => {
            setPainel(null);
          }}
        />
      )}

      {painel === 'eliminar' && (
        <Eliminacao
          pacienteId={paciente.id}
          cancelar={() => {
            setPainel(null);
          }}
        />
      )}
    </section>
  );
}

interface Formulario {
  nome: string;
  dataNascimento: string;
  responsavelNome: string;
  parentesco: string;
  contato: string;
}

type Erros = Partial<Record<keyof Formulario, string>>;

function validar(f: Formulario, hoje: string): Erros {
  const erros: Erros = {};
  if (!f.nome.trim()) erros.nome = 'Informe o nome.';
  if (!f.dataNascimento) erros.dataNascimento = 'Informe a data de nascimento.';
  else if (f.dataNascimento > hoje) erros.dataNascimento = 'A data de nascimento não pode estar no futuro.';
  if (f.dataNascimento && ehMenorDeIdade(f.dataNascimento, hoje)) {
    if (!f.responsavelNome.trim()) erros.responsavelNome = 'Informe o nome do responsável legal.';
    if (!f.parentesco) erros.parentesco = 'Escolha o parentesco.';
    if (!f.contato.trim()) erros.contato = 'Informe um contato do responsável.';
  }
  return erros;
}

interface PropsCorrecao {
  paciente: PacienteResumo;
  responsavel: Responsavel | null;
  concluir: (texto: string) => void;
  cancelar: () => void;
}

function FormularioCorrecao({ paciente, responsavel, concluir, cancelar }: PropsCorrecao) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  const refFormulario = useRef<HTMLFormElement>(null);
  const [f, setF] = useState<Formulario>({
    nome: paciente.nome ?? '',
    dataNascimento: paciente.dataNascimento ?? '',
    responsavelNome: responsavel?.nome ?? '',
    parentesco: responsavel?.parentesco ?? '',
    contato: responsavel?.contato ?? '',
  });
  const [erros, setErros] = useState<Erros>({});
  const hoje = hojeIso();

  useEffect(() => {
    refFormulario.current?.querySelector<HTMLInputElement>('input')?.focus();
  }, []);

  const correcao = useMutation({
    mutationFn: (dados: NovaIdentificacao) => corrigirIdentificacao(api, paciente.id, dados),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: chavesPacientes.todas });
      concluir('Identificação corrigida.');
    },
  });

  function alterar(campo: keyof Formulario, valor: string) {
    setF((atual) => ({ ...atual, [campo]: valor }));
    setErros((atuais) => Object.fromEntries(Object.entries(atuais).filter(([c]) => c !== campo)));
  }

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    const novos = validar(f, hoje);
    setErros(novos);
    if (Object.keys(novos).length > 0) return;
    const temResponsavel = f.responsavelNome.trim() !== '';
    correcao.mutate({
      nome: f.nome.trim(),
      dataNascimento: f.dataNascimento,
      responsavel: temResponsavel
        ? { nome: f.responsavelNome.trim(), parentesco: f.parentesco || null, contato: f.contato.trim() || null }
        : null,
    });
  }

  const erroApi = correcao.error instanceof ErroApi ? correcao.error : undefined;
  const erroDe = (campo: keyof Formulario, campoApi: string) => erros[campo] ?? erroApi?.doCampo(campoApi);

  return (
    <form ref={refFormulario} onSubmit={enviar} noValidate className={estilos.formulario} aria-label="Corrigir identificação">
      {correcao.error && <Mensagem tipo="erro">{mensagemDeErro(correcao.error)}</Mensagem>}
      <div className={estilos.linha}>
        <Campo
          rotulo="Nome"
          maxLength={150}
          value={f.nome}
          onChange={(e) => {
            alterar('nome', e.target.value);
          }}
          erro={erroDe('nome', 'Nome')}
        />
        <Campo
          rotulo="Data de nascimento"
          type="date"
          max={hoje}
          value={f.dataNascimento}
          onChange={(e) => {
            alterar('dataNascimento', e.target.value);
          }}
          erro={erroDe('dataNascimento', 'DataNascimento')}
        />
      </div>
      <Campo
        rotulo="Nome do responsável legal"
        dica="Obrigatório para menores de 18 anos."
        maxLength={150}
        value={f.responsavelNome}
        onChange={(e) => {
          alterar('responsavelNome', e.target.value);
        }}
        erro={erroDe('responsavelNome', 'Responsavel.Nome')}
      />
      <div className={estilos.linha}>
        <CampoSelecao
          rotulo="Parentesco"
          value={f.parentesco}
          onChange={(e) => {
            alterar('parentesco', e.target.value);
          }}
          erro={erroDe('parentesco', 'Responsavel.Parentesco')}
        >
          <option value="">Escolha</option>
          {PARENTESCOS.map((p) => (
            <option key={p.valor} value={p.valor}>
              {p.nome}
            </option>
          ))}
          {f.parentesco && !PARENTESCOS.some((p) => p.valor === f.parentesco) && <option value={f.parentesco}>{f.parentesco}</option>}
        </CampoSelecao>
        <Campo
          rotulo="Contato do responsável"
          maxLength={100}
          value={f.contato}
          onChange={(e) => {
            alterar('contato', e.target.value);
          }}
          erro={erroDe('contato', 'Responsavel.Contato')}
        />
      </div>
      <div className={estilos.acoes}>
        <Botao type="submit" carregando={correcao.isPending} textoCarregando="Salvando…">
          Salvar correção
        </Botao>
        <Botao variante="secundario" onClick={cancelar}>
          Cancelar
        </Botao>
      </div>
    </form>
  );
}

function Eliminacao({ pacienteId, cancelar }: { pacienteId: string; cancelar: () => void }) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  const refGrupo = useRef<HTMLFormElement>(null);
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | undefined>();

  useEffect(() => {
    refGrupo.current?.querySelector<HTMLInputElement>('input')?.focus();
  }, []);

  const eliminacao = useMutation({
    mutationFn: () => eliminarDados(api, pacienteId, texto.trim()),
    onSuccess: () => {
      // A página volta mostrando "Dados eliminados a pedido do titular".
      void queryClient.invalidateQueries({ queryKey: chavesPacientes.todas });
    },
  });

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    if (texto.trim() !== CONFIRMACAO) {
      setErro(`Digite ${CONFIRMACAO} para confirmar.`);
      return;
    }
    eliminacao.mutate();
  }

  return (
    <form ref={refGrupo} onSubmit={enviar} noValidate className={`${estilos.formulario} ${estilos.confirmacao}`} aria-label="Eliminar dados do paciente">
      {eliminacao.error && <Mensagem tipo="erro">{mensagemDeErro(eliminacao.error)}</Mensagem>}
      <p>
        <strong>Não dá para desfazer.</strong> Serão apagados o nome, a data de nascimento, o responsável, as imagens das
        digitais e as observações em texto livre. Ficam, sem identificação, os termos assinados, a situação diagnóstica e
        os resultados das análises, como prevê a regra de eliminação (RN08).
      </p>
      <Campo
        rotulo={`Digite ${CONFIRMACAO} para confirmar`}
        autoComplete="off"
        spellCheck={false}
        value={texto}
        onChange={(e) => {
          setTexto(e.target.value);
          setErro(undefined);
        }}
        erro={erro}
      />
      <div className={estilos.acoes}>
        <Botao type="submit" carregando={eliminacao.isPending} textoCarregando="Eliminando…">
          Eliminar os dados do paciente
        </Botao>
        <Botao variante="secundario" onClick={cancelar}>
          Cancelar
        </Botao>
      </div>
    </form>
  );
}
