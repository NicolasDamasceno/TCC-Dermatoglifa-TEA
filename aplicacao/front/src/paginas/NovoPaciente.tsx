import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRef, useState, type SubmitEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { ErroApi, mensagemDeErro } from '../api/erros';
import { cadastrarPaciente, chavesPacientes, type NovoPaciente as DadosPaciente, type Sexo } from '../api/pacientes';
import { Botao } from '../componentes/Botao';
import { Campo, CampoSelecao } from '../componentes/Campo';
import { Mensagem } from '../componentes/Mensagem';
import { Opcoes } from '../componentes/Opcoes';
import { useTituloPagina } from '../componentes/useTituloPagina';
import { ehMenorDeIdade, hojeIso, PARENTESCOS } from '../dominio/pacientes';
import { useSessao } from '../sessao/contextoSessao';
import estilos from './Formulario.module.css';

interface Formulario {
  nome: string;
  dataNascimento: string;
  sexo: Sexo | '';
  responsavelNome: string;
  parentesco: string;
  contato: string;
}

type Erros = Partial<Record<keyof Formulario, string>>;

const VAZIO: Formulario = { nome: '', dataNascimento: '', sexo: '', responsavelNome: '', parentesco: '', contato: '' };
const ORDEM: readonly (keyof Formulario)[] = ['nome', 'dataNascimento', 'sexo', 'responsavelNome', 'parentesco', 'contato'];

/** Estado passado para a tela do paciente logo após o cadastro. */
export interface EstadoPacienteCriado {
  criado: true;
}

function validar(f: Formulario, hoje: string): Erros {
  const erros: Erros = {};
  if (!f.nome.trim()) erros.nome = 'Informe o nome do paciente.';
  if (!f.dataNascimento) erros.dataNascimento = 'Informe a data de nascimento.';
  else if (f.dataNascimento > hoje) erros.dataNascimento = 'A data de nascimento não pode estar no futuro.';
  else if (Number(f.dataNascimento.slice(0, 4)) <= Number(hoje.slice(0, 4)) - 120)
    erros.dataNascimento = 'Data de nascimento inválida.';
  if (!f.sexo) erros.sexo = 'Escolha o sexo biológico.';

  if (f.dataNascimento && ehMenorDeIdade(f.dataNascimento, hoje)) {
    if (!f.responsavelNome.trim()) erros.responsavelNome = 'Informe o nome do responsável legal.';
    if (!f.parentesco) erros.parentesco = 'Escolha o parentesco.';
    if (!f.contato.trim()) erros.contato = 'Informe um contato do responsável.';
  }

  return erros;
}

/** UC04 — cadastro do paciente; o consentimento (UC05) vem logo em seguida, na tela do paciente. */
export function NovoPaciente() {
  useTituloPagina('Cadastrar paciente');
  const { api } = useSessao();
  const navegar = useNavigate();
  const queryClient = useQueryClient();
  const [formulario, setFormulario] = useState<Formulario>(VAZIO);
  const [erros, setErros] = useState<Erros>({});
  const refFormulario = useRef<HTMLFormElement>(null);
  const hoje = hojeIso();

  const menor = formulario.dataNascimento !== '' && ehMenorDeIdade(formulario.dataNascimento, hoje);

  const envio = useMutation({
    mutationFn: (dados: DadosPaciente) => cadastrarPaciente(api, dados),
    onSuccess: ({ id }) => {
      void queryClient.invalidateQueries({ queryKey: chavesPacientes.todas });
      const estado: EstadoPacienteCriado = { criado: true };
      void navegar(`/pacientes/${id}`, { state: estado });
    },
  });

  function alterar<K extends keyof Formulario>(campo: K, valor: Formulario[K]) {
    setFormulario((atual) => ({ ...atual, [campo]: valor }));
  }

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    const novosErros = validar(formulario, hoje);
    setErros(novosErros);

    const primeiro = ORDEM.find((campo) => novosErros[campo]);
    if (primeiro) {
      const alvo = refFormulario.current?.querySelector<HTMLElement>(`[name="${primeiro}"], [data-campo="${primeiro}"] input`);
      alvo?.focus();
      return;
    }

    const temResponsavel = formulario.responsavelNome.trim() !== '';
    envio.mutate({
      nome: formulario.nome.trim(),
      dataNascimento: formulario.dataNascimento,
      sexo: formulario.sexo as Sexo,
      responsavel: temResponsavel
        ? {
            nome: formulario.responsavelNome.trim(),
            parentesco: formulario.parentesco || null,
            contato: formulario.contato.trim() || null,
          }
        : null,
    });
  }

  const erroApi = envio.error instanceof ErroApi ? envio.error : undefined;
  const erroDe = (campo: keyof Formulario, campoApi: string) => erros[campo] ?? erroApi?.doCampo(campoApi);

  return (
    <section className={estilos.pagina} aria-labelledby="titulo-novo">
      <p className={estilos.voltar}>
        <Link to="/pacientes">Pacientes</Link>
      </p>
      <h1 id="titulo-novo">Cadastrar paciente</h1>
      <p className={estilos.introducao}>
        Só os dados necessários à pesquisa. Nome, nascimento e responsável ficam guardados à parte dos dados usados na
        análise.
      </p>

      {envio.error && <Mensagem tipo="erro">{mensagemDeErro(envio.error)}</Mensagem>}

      <form ref={refFormulario} onSubmit={enviar} noValidate className={estilos.formulario}>
        <fieldset className={estilos.grupo}>
          <legend>Paciente</legend>
          <Campo
            rotulo="Nome completo"
            name="nome"
            maxLength={150}
            autoComplete="off"
            value={formulario.nome}
            onChange={(e) => {
              alterar('nome', e.target.value);
            }}
            erro={erroDe('nome', 'Nome')}
          />
          <Campo
            rotulo="Data de nascimento"
            type="date"
            name="dataNascimento"
            max={hoje}
            value={formulario.dataNascimento}
            onChange={(e) => {
              alterar('dataNascimento', e.target.value);
            }}
            erro={erroDe('dataNascimento', 'DataNascimento')}
          />
          <div data-campo="sexo">
            <Opcoes<Sexo>
              legenda="Sexo biológico"
              dica="Entra na análise porque as contagens dermatoglíficas variam por sexo."
              emLinha
              opcoes={[
                { valor: 'FEMININO', rotulo: 'Feminino' },
                { valor: 'MASCULINO', rotulo: 'Masculino' },
              ]}
              valor={formulario.sexo}
              aoMudar={(valor) => {
                alterar('sexo', valor);
              }}
              erro={erroDe('sexo', 'Sexo')}
            />
          </div>
        </fieldset>

        <fieldset className={estilos.grupo}>
          <legend>{menor ? 'Responsável legal' : 'Responsável legal (opcional)'}</legend>
          <p className={estilos.nota} aria-live="polite">
            {menor
              ? 'Obrigatório: o paciente tem menos de 18 anos e o TCLE é assinado pelo responsável.'
              : 'Obrigatório para menores de 18 anos.'}
          </p>
          <Campo
            rotulo="Nome do responsável"
            name="responsavelNome"
            maxLength={150}
            autoComplete="off"
            required={menor}
            value={formulario.responsavelNome}
            onChange={(e) => {
              alterar('responsavelNome', e.target.value);
            }}
            erro={erroDe('responsavelNome', 'Responsavel.Nome')}
          />
          <div className={estilos.linha}>
            <CampoSelecao
              rotulo="Parentesco"
              name="parentesco"
              required={menor}
              value={formulario.parentesco}
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
            </CampoSelecao>
            <Campo
              rotulo="Contato (telefone ou e-mail)"
              name="contato"
              maxLength={100}
              autoComplete="off"
              required={menor}
              value={formulario.contato}
              onChange={(e) => {
                alterar('contato', e.target.value);
              }}
              erro={erroDe('contato', 'Responsavel.Contato')}
            />
          </div>
        </fieldset>

        <Botao type="submit" carregando={envio.isPending} textoCarregando="Cadastrando…">
          Cadastrar paciente
        </Botao>
      </form>
    </section>
  );
}
