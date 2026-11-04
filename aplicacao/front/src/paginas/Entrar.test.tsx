import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { CHAVE_SESSAO } from '../sessao/sessao';
import { ADMIN, json, MEDICO, renderizarApp, respostaEntrada, simularFetch } from '../testes/utilitarios';

async function preencherEEntrar(email = 'medica@exemplo.test', senha = 'Senha-sintetica-1') {
  const usuario = userEvent.setup();
  await usuario.type(screen.getByLabelText('E-mail'), email);
  await usuario.type(screen.getByLabelText('Senha'), senha);
  await usuario.click(screen.getByRole('button', { name: 'Entrar' }));
}

describe('Entrar (UC01)', () => {
  it('leva o médico à lista de pacientes e guarda a sessão', async () => {
    const fetchMock = simularFetch(json(200, respostaEntrada(MEDICO)));
    renderizarApp('/entrar');

    await preencherEEntrar();

    expect(await screen.findByRole('heading', { name: 'Pacientes', level: 1 })).toBeInTheDocument();
    expect(screen.getByText(MEDICO.nome)).toBeInTheDocument();
    expect(sessionStorage.getItem(CHAVE_SESSAO)).toContain('token-MEDICO');

    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('/api/auth/login');
    expect(JSON.parse(init!.body as string)).toEqual({ email: 'medica@exemplo.test', senha: 'Senha-sintetica-1' });
  });

  it('leva o administrador aos cadastros pendentes', async () => {
    simularFetch(json(200, respostaEntrada(ADMIN)), json(200, []));
    renderizarApp('/entrar');

    await preencherEEntrar('admin@exemplo.test');

    expect(await screen.findByRole('heading', { name: 'Cadastros pendentes', level: 1 })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Cadastros pendentes' })).toHaveAttribute('aria-current', 'page');
  });

  it('volta para a página pedida antes do login', async () => {
    simularFetch(json(200, respostaEntrada(MEDICO)));
    renderizarApp('/sessoes/abc');

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
    await preencherEEntrar();

    expect(await screen.findByRole('heading', { name: 'Coleta' })).toBeInTheDocument();
  });

  it.each([
    [401, 'E-mail ou senha incorretos.'],
    [403, 'Cadastro aguardando aprovação.'],
    [423, 'Muitas tentativas sem sucesso. Tente novamente em alguns minutos.'],
    [429, 'Muitas tentativas. Aguarde um minuto e tente novamente.'],
  ])('mostra a mensagem da API para %i', async (status, titulo) => {
    simularFetch(json(status, { title: titulo, status }));
    renderizarApp('/entrar');

    await preencherEEntrar();

    expect(await screen.findByRole('alert')).toHaveTextContent(titulo);
    expect(screen.getByRole('heading', { name: 'Entrar' })).toBeInTheDocument();
    expect(sessionStorage.getItem(CHAVE_SESSAO)).toBeNull();
  });

  it('mostra erro do campo vindo da validação da API', async () => {
    simularFetch(json(400, { title: 'Dados inválidos', errors: { Email: ['Informe um e-mail válido.'] } }));
    renderizarApp('/entrar');

    await preencherEEntrar('nao-e-email');

    const campo = screen.getByLabelText('E-mail');
    await waitFor(() => {
      expect(campo).toHaveAccessibleDescription('Informe um e-mail válido.');
    });
    expect(campo).toHaveAttribute('aria-invalid', 'true');
  });

  it('valida campos vazios sem chamar a API', async () => {
    const fetchMock = simularFetch();
    renderizarApp('/entrar');

    await userEvent.setup().click(screen.getByRole('button', { name: 'Entrar' }));

    expect(screen.getByLabelText('E-mail')).toHaveAccessibleDescription('Informe o e-mail.');
    expect(screen.getByLabelText('Senha')).toHaveAccessibleDescription('Informe a senha.');
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('explica quando o servidor não responde', async () => {
    simularFetch(new TypeError('Failed to fetch'));
    renderizarApp('/entrar');

    await preencherEEntrar();

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível falar com o servidor');
  });

  it('não mostra a ficha decorativa a leitores de tela', () => {
    renderizarApp('/entrar');
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });
});
