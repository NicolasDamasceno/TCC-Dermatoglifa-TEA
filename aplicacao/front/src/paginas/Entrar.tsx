import { useMutation } from '@tanstack/react-query';
import { useState, type SubmitEvent } from 'react';
import { Link, Navigate, useLocation } from 'react-router';
import { entrar } from '../api/autenticacao';
import { ErroApi, mensagemDeErro } from '../api/erros';
import { Botao } from '../componentes/Botao';
import { Campo } from '../componentes/Campo';
import { FichaDecadactilar } from '../componentes/FichaDecadactilar';
import { Mensagem } from '../componentes/Mensagem';
import { useTituloPagina } from '../componentes/useTituloPagina';
import { useSessao } from '../sessao/contextoSessao';
import { destinoSeguro, paginaInicialDo } from '../sessao/destinos';
import type { EstadoEntrar } from '../sessao/RotaProtegida';
import estilos from './Entrar.module.css';

interface ErrosLocais {
  email?: string;
  senha?: string;
}

/** UC01 — Entrar. As mensagens da API (senha errada, pendente, bloqueio) aparecem como vêm. */
export function Entrar() {
  useTituloPagina('Entrar');
  const { sessao, api, motivoSaida, iniciar } = useSessao();
  const local = useLocation();
  const destinoPedido = destinoSeguro((local.state as EstadoEntrar | null)?.de);

  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [errosLocais, setErrosLocais] = useState<ErrosLocais>({});

  const login = useMutation({
    mutationFn: () => entrar(api, email.trim(), senha),
    onSuccess: iniciar,
  });

  // Logado (agora ou antes): volta à página pedida ou vai à tela do papel.
  if (sessao) {
    return <Navigate to={destinoPedido ?? paginaInicialDo(sessao.usuario.papel)} replace />;
  }

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    const erros: ErrosLocais = {};
    if (!email.trim()) erros.email = 'Informe o e-mail.';
    if (!senha) erros.senha = 'Informe a senha.';
    setErrosLocais(erros);
    if (erros.email || erros.senha) return;
    login.mutate();
  }

  const erroApi = login.error instanceof ErroApi ? login.error : undefined;

  return (
    <div className={estilos.pagina}>
      <section className={estilos.formulario} aria-labelledby="titulo-entrar">
        <h1 id="titulo-entrar">Entrar</h1>
        <p className={estilos.introducao}>
          Acesso para médicos com cadastro aprovado e para a administração do protocolo.
        </p>

        {motivoSaida === 'expirou' && !login.error && (
          <Mensagem>Sua sessão terminou. Entre de novo para continuar.</Mensagem>
        )}
        {motivoSaida === 'saiu' && !login.error && <Mensagem>Você saiu do sistema.</Mensagem>}
        {login.error && <Mensagem tipo="erro">{mensagemDeErro(login.error)}</Mensagem>}

        <form onSubmit={enviar} noValidate className={estilos.campos}>
          <Campo
            rotulo="E-mail"
            type="email"
            name="email"
            autoComplete="username"
            value={email}
            onChange={(e) => {
              setEmail(e.target.value);
            }}
            erro={errosLocais.email ?? erroApi?.doCampo('Email')}
          />
          <Campo
            rotulo="Senha"
            type="password"
            name="senha"
            autoComplete="current-password"
            value={senha}
            onChange={(e) => {
              setSenha(e.target.value);
            }}
            erro={errosLocais.senha ?? erroApi?.doCampo('Senha')}
          />
          <Botao type="submit" carregando={login.isPending} textoCarregando="Entrando…">
            Entrar
          </Botao>
        </form>

        <p className={estilos.cadastro}>
          Ainda não tem acesso? <Link to="/cadastro">Solicitar cadastro de médico</Link>
        </p>
      </section>

      <FichaDecadactilar decorativa className={estilos.ficha} />
    </div>
  );
}
