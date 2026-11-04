import { useMutation } from '@tanstack/react-query';
import { useEffect, useRef, useState, type SubmitEvent } from 'react';
import { Link, Navigate } from 'react-router';
import { ErroApi, mensagemDeErro } from '../api/erros';
import { solicitarCadastro, type DadosSolicitacao } from '../api/medicos';
import { Botao } from '../componentes/Botao';
import { Campo, CampoSelecao } from '../componentes/Campo';
import { Mensagem } from '../componentes/Mensagem';
import { useTituloPagina } from '../componentes/useTituloPagina';
import { problemaDaSenha, REGRA_SENHA, UFS } from '../dominio/medicos';
import { useSessao } from '../sessao/contextoSessao';
import { paginaInicialDo } from '../sessao/destinos';
import estilos from './Formulario.module.css';

interface Formulario extends DadosSolicitacao {
  repeticaoSenha: string;
}

type Erros = Partial<Record<keyof Formulario, string>>;

const VAZIO: Formulario = { nome: '', email: '', senha: '', repeticaoSenha: '', crm: '', ufCrm: '', especialidade: '' };

/** Ordem dos campos na tela, para levar o foco ao primeiro com erro. */
const ORDEM: readonly (keyof Formulario)[] = ['nome', 'email', 'senha', 'repeticaoSenha', 'crm', 'ufCrm', 'especialidade'];

function validar(f: Formulario): Erros {
  const erros: Erros = {};
  if (!f.nome.trim()) erros.nome = 'Informe o nome.';
  if (!f.email.trim()) erros.email = 'Informe o e-mail.';
  else if (!/^[^\s@]+@[^\s@]+$/.test(f.email.trim())) erros.email = 'E-mail inválido.';
  const senha = problemaDaSenha(f.senha);
  if (senha) erros.senha = senha;
  if (f.repeticaoSenha !== f.senha) erros.repeticaoSenha = 'As senhas não são iguais.';
  if (!f.crm.trim()) erros.crm = 'Informe o CRM.';
  else if (!/^\d+$/.test(f.crm.trim())) erros.crm = 'O CRM deve conter apenas números.';
  if (!f.ufCrm) erros.ufCrm = 'Escolha a UF do CRM.';
  if (!f.especialidade.trim()) erros.especialidade = 'Informe a especialidade.';
  return erros;
}

/** UC02 — o médico pede acesso; a administração do protocolo aprova ou recusa (UC03). */
export function SolicitarCadastro() {
  useTituloPagina('Solicitar cadastro de médico');
  const { sessao, api } = useSessao();
  const [formulario, setFormulario] = useState<Formulario>(VAZIO);
  const [erros, setErros] = useState<Erros>({});
  const refFormulario = useRef<HTMLFormElement>(null);

  const envio = useMutation({
    mutationFn: (dados: DadosSolicitacao) => solicitarCadastro(api, dados),
    onSuccess: () => {
      setFormulario(VAZIO); // a senha não fica na memória da tela
    },
  });

  if (sessao) {
    return <Navigate to={paginaInicialDo(sessao.usuario.papel)} replace />;
  }

  if (envio.isSuccess) {
    return <PedidoEnviado />;
  }

  function alterar(campo: keyof Formulario) {
    return (evento: { target: { value: string } }) => {
      setFormulario((atual) => ({ ...atual, [campo]: evento.target.value }));
    };
  }

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    const novosErros = validar(formulario);
    setErros(novosErros);

    const primeiro = ORDEM.find((campo) => novosErros[campo]);
    if (primeiro) {
      const elemento = refFormulario.current?.elements.namedItem(primeiro);
      if (elemento instanceof HTMLElement) elemento.focus();
      return;
    }

    envio.mutate({
      nome: formulario.nome.trim(),
      email: formulario.email.trim(),
      senha: formulario.senha,
      crm: formulario.crm.trim(),
      ufCrm: formulario.ufCrm,
      especialidade: formulario.especialidade.trim(),
    });
  }

  const erroApi = envio.error instanceof ErroApi ? envio.error : undefined;
  const erroDe = (campo: keyof DadosSolicitacao) => erros[campo] ?? erroApi?.doCampo(campo);

  return (
    <section className={estilos.pagina} aria-labelledby="titulo-cadastro">
      <h1 id="titulo-cadastro">Solicitar cadastro de médico</h1>
      <p className={estilos.introducao}>
        O acesso é liberado depois que a administração do protocolo confere o seu CRM e o seu vínculo com a pesquisa
        aprovada pelo CEP.
      </p>

      {envio.error && <Mensagem tipo="erro">{mensagemDeErro(envio.error)}</Mensagem>}

      <form ref={refFormulario} onSubmit={enviar} noValidate className={estilos.formulario}>
        <fieldset className={estilos.grupo}>
          <legend>Acesso</legend>
          <Campo
            rotulo="Nome completo"
            name="nome"
            autoComplete="name"
            maxLength={150}
            value={formulario.nome}
            onChange={alterar('nome')}
            erro={erroDe('nome')}
          />
          <Campo
            rotulo="E-mail"
            type="email"
            name="email"
            autoComplete="email"
            maxLength={256}
            value={formulario.email}
            onChange={alterar('email')}
            erro={erroDe('email')}
          />
          <Campo
            rotulo="Senha"
            type="password"
            name="senha"
            autoComplete="new-password"
            maxLength={128}
            dica={REGRA_SENHA}
            value={formulario.senha}
            onChange={alterar('senha')}
            erro={erroDe('senha')}
          />
          <Campo
            rotulo="Repita a senha"
            type="password"
            name="repeticaoSenha"
            autoComplete="new-password"
            maxLength={128}
            value={formulario.repeticaoSenha}
            onChange={alterar('repeticaoSenha')}
            erro={erros.repeticaoSenha}
          />
        </fieldset>

        <fieldset className={estilos.grupo}>
          <legend>Registro profissional</legend>
          <div className={estilos.linha}>
            <Campo
              rotulo="CRM"
              name="crm"
              inputMode="numeric"
              maxLength={10}
              value={formulario.crm}
              onChange={alterar('crm')}
              erro={erroDe('crm')}
            />
            <CampoSelecao
              rotulo="UF do CRM"
              name="ufCrm"
              value={formulario.ufCrm}
              onChange={alterar('ufCrm')}
              erro={erroDe('ufCrm')}
            >
              <option value="">Escolha</option>
              {UFS.map((uf) => (
                <option key={uf.sigla} value={uf.sigla}>
                  {uf.sigla} – {uf.nome}
                </option>
              ))}
            </CampoSelecao>
          </div>
          <Campo
            rotulo="Especialidade"
            name="especialidade"
            maxLength={100}
            value={formulario.especialidade}
            onChange={alterar('especialidade')}
            erro={erroDe('especialidade')}
          />
        </fieldset>

        <Botao type="submit" carregando={envio.isPending} textoCarregando="Enviando…">
          Enviar pedido
        </Botao>
      </form>

      <p className={estilos.rodape}>
        Já tem acesso? <Link to="/entrar">Entrar</Link>
      </p>
    </section>
  );
}

function PedidoEnviado() {
  const titulo = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    titulo.current?.focus(); // leitor de tela anuncia o resultado do envio
  }, []);

  return (
    <section className={estilos.pagina} aria-labelledby="titulo-enviado">
      <h1 id="titulo-enviado" ref={titulo} tabIndex={-1}>
        Pedido enviado
      </h1>
      <p>Seu cadastro está aguardando aprovação. Você poderá entrar assim que a administração do protocolo aprovar o pedido.</p>
      <p>
        <Link to="/entrar">Ir para Entrar</Link>
      </p>
    </section>
  );
}
