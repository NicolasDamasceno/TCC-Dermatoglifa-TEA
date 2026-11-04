import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ADMIN, comSessao, json, MEDICO, Provedores, renderizarApp, simularFetch } from '../testes/utilitarios';
import { App } from '../App';
import { useSessao } from './contextoSessao';
import { CHAVE_SESSAO } from './sessao';

describe('rotas protegidas e sessão', () => {
  it('manda quem não entrou para /entrar', async () => {
    renderizarApp('/pacientes');
    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
  });

  it('leva "/" à tela do papel', async () => {
    comSessao(ADMIN);
    simularFetch(json(200, []));
    renderizarApp('/');
    expect(await screen.findByRole('heading', { name: 'Cadastros pendentes', level: 1 })).toBeInTheDocument();
  });

  it('nega a tela de outro papel sem revelar o conteúdo', async () => {
    comSessao(ADMIN);
    renderizarApp('/pacientes');
    expect(await screen.findByRole('heading', { name: 'Acesso não permitido' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Pacientes' })).not.toBeInTheDocument();
  });

  it('quem já entrou e abre /entrar vai para a sua tela', async () => {
    comSessao(MEDICO);
    simularFetch(json(200, []));
    renderizarApp('/entrar');
    expect(await screen.findByRole('heading', { name: 'Pacientes', level: 1 })).toBeInTheDocument();
  });

  it('mostra a faixa de pesquisa em todas as telas, inclusive Entrar', () => {
    renderizarApp('/entrar');
    expect(screen.getByRole('complementary', { name: 'Aviso de pesquisa' })).toHaveTextContent(
      'Não é ferramenta de diagnóstico',
    );
  });

  it('sai ao clicar em Sair e limpa a sessão', async () => {
    comSessao(MEDICO);
    simularFetch(json(200, []));
    renderizarApp('/pacientes');

    await userEvent.setup().click(screen.getByRole('button', { name: 'Sair' }));

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Você saiu do sistema.');
    expect(sessionStorage.getItem(CHAVE_SESSAO)).toBeNull();
  });

  it('sai sozinho quando o token expira', () => {
    vi.useFakeTimers();
    comSessao(MEDICO, 5_000);
    simularFetch(json(200, []));
    renderizarApp('/pacientes');
    expect(screen.getByRole('heading', { name: 'Pacientes', level: 1 })).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(5_001);
    });

    expect(screen.getByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Sua sessão terminou');
    expect(sessionStorage.getItem(CHAVE_SESSAO)).toBeNull();
  });

  it('sai em qualquer 401 da API', async () => {
    comSessao(MEDICO);
    simularFetch(json(401, { title: 'Autenticação necessária. Faça login novamente.' }));

    function ChamaApi() {
      const { api } = useSessao();
      return (
        <button type="button" onClick={() => void api.requisitar('GET', '/api/pacientes').catch(() => undefined)}>
          Chamar API
        </button>
      );
    }

    // Rota sem consultas próprias: a única chamada é a do botão.
    render(
      <Provedores rota="/pacientes/novo">
        <ChamaApi />
        <App />
      </Provedores>,
    );

    await userEvent.setup().click(screen.getByRole('button', { name: 'Chamar API' }));

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Sua sessão terminou');
    expect(sessionStorage.getItem(CHAVE_SESSAO)).toBeNull();
  });

  it('responde com página não encontrada para rota desconhecida', () => {
    renderizarApp('/nao-existe');
    expect(screen.getByRole('heading', { name: 'Página não encontrada' })).toBeInTheDocument();
  });
});
