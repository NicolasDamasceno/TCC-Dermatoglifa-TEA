import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type SubmitEvent } from 'react';
import { ErroApi, mensagemDeErro } from '../../api/erros';
import {
  chavesPacientes,
  registrarConsentimento,
  revogarConsentimento,
  type Consentimento,
  type NovoConsentimento,
  type TipoTermo,
} from '../../api/pacientes';
import { Botao } from '../../componentes/Botao';
import { Campo } from '../../componentes/Campo';
import { data } from '../../componentes/formatos';
import { Mensagem } from '../../componentes/Mensagem';
import { Marcacao, Opcoes } from '../../componentes/Opcoes';
import { hojeIso } from '../../dominio/pacientes';
import { useSessao } from '../../sessao/contextoSessao';
import estilos from './Paciente.module.css';

interface PropsSecao {
  pacienteId: string;
  consentimentos: Consentimento[];
  registrando: boolean;
  setRegistrando: (aberto: boolean) => void;
  /** Registrar termo é do médico; revogar, a pedido do titular, também do administrador. */
  podeRegistrar?: boolean;
}

/** UC05 — termos assinados: registrar (TCLE/TALE) e revogar. */
export function SecaoConsentimentos({ pacienteId, consentimentos, registrando, setRegistrando, podeRegistrar = true }: PropsSecao) {
  const [aviso, setAviso] = useState<string | null>(null);
  const [revogando, setRevogando] = useState<string | null>(null);
  const refSecao = useRef<HTMLElement>(null);

  // Ao abrir o formulário, o foco (e a rolagem) vai para a primeira opção.
  useEffect(() => {
    if (registrando) {
      refSecao.current?.querySelector<HTMLInputElement>('form input')?.focus();
    }
  }, [registrando]);

  return (
    <section ref={refSecao} className={estilos.secao} aria-labelledby="titulo-consentimentos">
      <div className={estilos.cabecalhoSecao}>
        <h2 id="titulo-consentimentos">Consentimentos</h2>
        {podeRegistrar && !registrando && (
          <Botao
            variante="secundario"
            onClick={() => {
              setAviso(null);
              setRegistrando(true);
            }}
          >
            Registrar consentimento
          </Botao>
        )}
      </div>

      {aviso && <Mensagem>{aviso}</Mensagem>}

      {registrando && (
        <FormularioConsentimento
          pacienteId={pacienteId}
          concluir={(texto) => {
            setRegistrando(false);
            setAviso(texto);
          }}
          cancelar={() => {
            setRegistrando(false);
          }}
        />
      )}

      {consentimentos.length === 0 ? (
        <p className={estilos.nota}>Nenhum termo registrado.</p>
      ) : (
        <div className={estilos.rolagem} role="region" aria-label="Tabela de consentimentos" tabIndex={0}>
          <table className={estilos.tabela}>
            <thead>
              <tr>
                <th scope="col">Termo</th>
                <th scope="col">Assinado em</th>
                <th scope="col">Autorizações</th>
                <th scope="col">Situação</th>
                <th scope="col">
                  <span className="somente-leitor-de-tela">Ação</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {consentimentos.map((c) => (
                <LinhaConsentimento
                  key={c.id}
                  pacienteId={pacienteId}
                  consentimento={c}
                  confirmando={revogando === c.id}
                  alternar={(aberto) => {
                    setAviso(null);
                    setRevogando(aberto ? c.id : null);
                  }}
                  concluir={(texto) => {
                    setRevogando(null);
                    setAviso(texto);
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

function Autorizacoes({ c }: { c: Consentimento }) {
  if (c.tipo === 'TALE') {
    return <span>Assentimento da criança ou do adolescente</span>;
  }

  const sim = (v: boolean) => (v ? 'sim' : 'não');
  return (
    <ul className={estilos.autorizacoes}>
      <li>Análise das digitais: {sim(c.autorizaAnalise)}</li>
      <li>Ciência do uso de IA: {sim(c.cienteUsoIA)}</li>
      <li>Uso em treino de modelos: {sim(c.autorizaUsoTreino)}</li>
    </ul>
  );
}

interface PropsLinha {
  pacienteId: string;
  consentimento: Consentimento;
  confirmando: boolean;
  alternar: (aberto: boolean) => void;
  concluir: (texto: string) => void;
}

function LinhaConsentimento({ pacienteId, consentimento: c, confirmando, alternar, concluir }: PropsLinha) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  const descricao = `${c.tipo} de ${data(c.assinadoEm)}`;

  const revogacao = useMutation({
    mutationFn: () => revogarConsentimento(api, pacienteId, c.id),
    onSuccess: (r) => {
      void queryClient.invalidateQueries({ queryKey: chavesPacientes.todas });
      concluir(
        r.podeColetar
          ? `${descricao} revogado. Outro termo vigente mantém a coleta liberada.`
          : `${descricao} revogado. A coleta não está mais liberada.`,
      );
    },
    onError: (erro) => {
      if (erro instanceof ErroApi && erro.status === 422) {
        void queryClient.invalidateQueries({ queryKey: chavesPacientes.detalhe(pacienteId) });
      }
    },
  });

  return (
    <>
      <tr className={confirmando ? estilos.linhaAberta : undefined}>
        <th scope="row">
          <span className={estilos.termo}>{c.tipo}</span>{' '}
          <span className={estilos.versao}>versão {c.versaoTermo}</span>
        </th>
        <td>{data(c.assinadoEm)}</td>
        <td>
          <Autorizacoes c={c} />
        </td>
        <td>{c.vigente ? 'Vigente' : `Revogado em ${data(c.revogadoEm)}`}</td>
        <td className={estilos.acao}>
          {c.vigente && !confirmando && (
            <Botao
              variante="secundario"
              aria-label={`Revogar ${descricao}`}
              onClick={() => {
                alternar(true);
              }}
            >
              Revogar
            </Botao>
          )}
        </td>
      </tr>
      {confirmando && (
        <tr>
          <td colSpan={5}>
            <div className={estilos.confirmacao} role="group" aria-label={`Confirmar revogação do ${descricao}`}>
              {revogacao.error && <Mensagem tipo="erro">{mensagemDeErro(revogacao.error)}</Mensagem>}
              <p>
                Revogar o {descricao}? Registre só se o titular pediu. Uma coleta em andamento não poderá continuar
                sem outro termo vigente.
              </p>
              <div className={estilos.acoes}>
                <Botao
                  carregando={revogacao.isPending}
                  textoCarregando="Revogando…"
                  onClick={() => {
                    revogacao.mutate();
                  }}
                >
                  Confirmar revogação
                </Botao>
                <Botao
                  variante="secundario"
                  onClick={() => {
                    alternar(false);
                  }}
                >
                  Manter o termo
                </Botao>
              </div>
            </div>
          </td>
        </tr>
      )}
    </>
  );
}

interface Formulario {
  tipo: TipoTermo | '';
  versaoTermo: string;
  assinadoEm: string;
  autorizaAnalise: boolean;
  cienteUsoIA: boolean;
  autorizaUsoTreino: boolean;
}

type Erros = Partial<Record<'tipo' | 'versaoTermo' | 'assinadoEm', string>>;

const VAZIO: Formulario = {
  tipo: '',
  versaoTermo: '',
  assinadoEm: '',
  autorizaAnalise: false,
  cienteUsoIA: false,
  autorizaUsoTreino: false,
};

interface PropsFormulario {
  pacienteId: string;
  concluir: (texto: string) => void;
  cancelar: () => void;
}

function FormularioConsentimento({ pacienteId, concluir, cancelar }: PropsFormulario) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  const [f, setF] = useState<Formulario>(VAZIO);
  const [erros, setErros] = useState<Erros>({});
  const hoje = hojeIso();

  const registro = useMutation({
    mutationFn: (dados: NovoConsentimento) => registrarConsentimento(api, pacienteId, dados),
    onSuccess: (r, dados) => {
      void queryClient.invalidateQueries({ queryKey: chavesPacientes.todas });
      concluir(
        r.podeColetar
          ? `${dados.tipo} registrado. Coleta liberada.`
          : dados.tipo === 'TALE'
            ? 'TALE registrado. A coleta é liberada pelo TCLE do responsável.'
            : 'TCLE registrado, mas a coleta continua bloqueada: o termo não autoriza a análise ou não registra a ciência do uso de IA.',
      );
    },
  });

  function alterar<K extends keyof Formulario>(campo: K, valor: Formulario[K]) {
    setF((atual) => ({ ...atual, [campo]: valor }));
  }

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    const novos: Erros = {};
    if (!f.tipo) novos.tipo = 'Escolha o tipo de termo.';
    if (!f.versaoTermo.trim()) novos.versaoTermo = 'Informe a versão do termo.';
    if (!f.assinadoEm) novos.assinadoEm = 'Informe a data de assinatura.';
    else if (f.assinadoEm > hoje) novos.assinadoEm = 'A data de assinatura não pode estar no futuro.';
    setErros(novos);
    if (!f.tipo || Object.keys(novos).length > 0) return;

    const tcle = f.tipo === 'TCLE';
    registro.mutate({
      tipo: f.tipo,
      versaoTermo: f.versaoTermo.trim(),
      assinadoEm: f.assinadoEm,
      // As autorizações só valem no TCLE; o TALE registra o assentimento (doc 08).
      autorizaAnalise: tcle && f.autorizaAnalise,
      cienteUsoIA: tcle && f.cienteUsoIA,
      autorizaUsoTreino: tcle && f.autorizaUsoTreino,
    });
  }

  const erroApi = registro.error instanceof ErroApi ? registro.error : undefined;
  const tcle = f.tipo === 'TCLE';
  const naoLibera = tcle && !(f.autorizaAnalise && f.cienteUsoIA);

  return (
    <form onSubmit={enviar} noValidate className={estilos.formulario} aria-label="Registrar consentimento">
      {registro.error && <Mensagem tipo="erro">{mensagemDeErro(registro.error)}</Mensagem>}

      <Opcoes<TipoTermo>
        legenda="Tipo de termo"
        opcoes={[
          {
            valor: 'TCLE',
            rotulo: 'TCLE: Termo de Consentimento Livre e Esclarecido',
            descricao: 'Assinado pelo paciente adulto ou pelo responsável legal. É o que libera a coleta.',
          },
          {
            valor: 'TALE',
            rotulo: 'TALE: Termo de Assentimento Livre e Esclarecido',
            descricao: 'Assinado pela criança ou pelo adolescente, junto com o TCLE do responsável.',
          },
        ]}
        valor={f.tipo}
        aoMudar={(valor) => {
          alterar('tipo', valor);
        }}
        erro={erros.tipo ?? erroApi?.doCampo('Tipo')}
      />

      <div className={estilos.linha}>
        <Campo
          rotulo="Versão do termo"
          dica="Como aprovada pelo CEP, por exemplo CEP-v1.0."
          maxLength={50}
          value={f.versaoTermo}
          onChange={(e) => {
            alterar('versaoTermo', e.target.value);
          }}
          erro={erros.versaoTermo ?? erroApi?.doCampo('VersaoTermo')}
        />
        <Campo
          rotulo="Data de assinatura"
          dica="Dia em que o termo foi assinado."
          type="date"
          max={hoje}
          value={f.assinadoEm}
          onChange={(e) => {
            alterar('assinadoEm', e.target.value);
          }}
          erro={erros.assinadoEm ?? erroApi?.doCampo('AssinadoEm')}
        />
      </div>

      {tcle && (
        <fieldset className={estilos.autorizacoesGrupo}>
          <legend>O que o TCLE assinado autoriza</legend>
          <p className={estilos.nota}>Marque exatamente o que consta no termo. Cada item é independente.</p>
          <Marcacao
            rotulo="Autoriza a análise das digitais pelo sistema"
            descricao="E reconhece que a análise não é diagnóstico. Necessário para coletar."
            marcado={f.autorizaAnalise}
            aoMudar={(v) => {
              alterar('autorizaAnalise', v);
            }}
          />
          <Marcacao
            rotulo="Está ciente do uso de IA como apoio ao médico"
            descricao="E de que pode recusar esse uso. Necessário para coletar."
            marcado={f.cienteUsoIA}
            aoMudar={(v) => {
              alterar('cienteUsoIA', v);
            }}
          />
          <Marcacao
            rotulo="Autoriza o uso das digitais no treino e na validação de modelos"
            descricao="Opcional. Sem esta autorização, as digitais nunca entram em treino."
            marcado={f.autorizaUsoTreino}
            aoMudar={(v) => {
              alterar('autorizaUsoTreino', v);
            }}
          />
          {naoLibera && (
            <p className={estilos.alertaLibera} aria-live="polite">
              Sem as duas primeiras marcações, o termo é registrado mas a coleta continua bloqueada.
            </p>
          )}
        </fieldset>
      )}

      <div className={estilos.acoes}>
        <Botao type="submit" carregando={registro.isPending} textoCarregando="Registrando…">
          {f.tipo ? `Registrar ${f.tipo}` : 'Registrar termo'}
        </Botao>
        <Botao variante="secundario" onClick={cancelar}>
          Cancelar
        </Botao>
      </div>
    </form>
  );
}
