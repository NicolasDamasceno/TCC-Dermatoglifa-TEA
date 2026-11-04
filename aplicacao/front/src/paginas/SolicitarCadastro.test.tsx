import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { comSessao, json, MEDICO, renderizarApp, simularFetch } from '../testes/utilitarios';

/** Dados sintéticos, nunca de uma pessoa real. */
const DADOS = {
  nome: 'Dra. Teste Sintética',
  email: 'teste@exemplo.test',
  senha: 'senha-sintetica-1',
  crm: '123456',
  ufCrm: 'SP',
  especialidade: 'Neuropediatria',
};

async function preencher(valores: Partial<typeof DADOS & { repeticao: string }> = {}) {
  const v = { ...DADOS, repeticao: valores.senha ?? DADOS.senha, ...valores };
  const usuario = userEvent.setup();
  await usuario.type(screen.getByLabelText('Nome completo'), `  ${v.nome} `);
  await usuario.type(screen.getByLabelText('E-mail'), v.email);
  await usuario.type(screen.getByLabelText('Senha'), v.senha);
  await usuario.type(screen.getByLabelText('Repita a senha'), v.repeticao);
  await usuario.type(screen.getByLabelText('CRM'), v.crm);
  await usuario.selectOptions(screen.getByLabelText('UF do CRM'), v.ufCrm);
  await usuario.type(screen.getByLabelText('Especialidade'), v.especialidade);
  return usuario;
}

describe('Solicitar cadastro (UC02)', () => {
  it('envia o pedido e explica que o acesso depende de aprovação', async () => {
    const fetchMock = simularFetch(json(201, { id: 'x', status: 'PENDENTE' }));
    renderizarApp('/cadastro');

    const usuario = await preencher();
    await usuario.click(screen.getByRole('button', { name: 'Enviar pedido' }));

    const titulo = await screen.findByRole('heading', { name: 'Pedido enviado' });
    expect(titulo).toHaveFocus();
    expect(screen.getByText(/aguardando aprovação/)).toBeInTheDocument();

    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('/api/medicos/solicitacoes');
    expect(JSON.parse(init!.body as string)).toEqual(DADOS); // nome aparado, sem a repetição da senha
  });

  it('valida no navegador, mostra cada erro no campo e leva o foco ao primeiro', async () => {
    const fetchMock = simularFetch();
    renderizarApp('/cadastro');

    await userEvent.setup().click(screen.getByRole('button', { name: 'Enviar pedido' }));

    expect(screen.getByLabelText('Nome completo')).toHaveFocus();
    expect(screen.getByLabelText('Nome completo')).toHaveAccessibleDescription('Informe o nome.');
    expect(screen.getByLabelText('UF do CRM')).toHaveAccessibleDescription('Escolha a UF do CRM.');
    expect(screen.getByLabelText('Senha')).toHaveAccessibleDescription(
      'Pelo menos 10 caracteres, com uma letra minúscula e um número. A senha deve ter pelo menos 10 caracteres.',
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([
    [{ senha: 'semnumeros-aqui' }, 'Senha', 'A senha deve conter pelo menos um número.'],
    [{ senha: 'SOMENTE-MAIUSCULA-1' }, 'Senha', 'A senha deve conter pelo menos uma letra minúscula.'],
    [{ repeticao: 'outra-senha-123' }, 'Repita a senha', 'As senhas não são iguais.'],
    [{ crm: '12A45' }, 'CRM', 'O CRM deve conter apenas números.'],
    [{ email: 'sem-arroba' }, 'E-mail', 'E-mail inválido.'],
  ])('recusa %o', async (valores, rotulo, mensagem) => {
    const fetchMock = simularFetch();
    renderizarApp('/cadastro');

    const usuario = await preencher(valores);
    await usuario.click(screen.getByRole('button', { name: 'Enviar pedido' }));

    const campo = screen.getByLabelText(rotulo);
    expect(campo).toHaveFocus();
    expect(campo).toHaveAttribute('aria-invalid', 'true');
    expect(campo.getAttribute('aria-describedby')).toBeTruthy();
    expect(screen.getByText(mensagem)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('mostra o conflito de e-mail ou CRM vindo da API', async () => {
    simularFetch(json(409, { title: 'Já existe cadastro com este CRM nesta UF.', status: 409 }));
    renderizarApp('/cadastro');

    const usuario = await preencher();
    await usuario.click(screen.getByRole('button', { name: 'Enviar pedido' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Já existe cadastro com este CRM nesta UF.');
    expect(screen.getByLabelText('CRM')).toHaveValue('123456'); // o formulário continua preenchido
  });

  it('liga ao campo os erros de validação da API', async () => {
    simularFetch(json(400, { title: 'Dados inválidos', errors: { UfCrm: ['UF do CRM inválida.'] } }));
    renderizarApp('/cadastro');

    const usuario = await preencher();
    await usuario.click(screen.getByRole('button', { name: 'Enviar pedido' }));

    await waitFor(() => {
      expect(screen.getByLabelText('UF do CRM')).toHaveAccessibleDescription('UF do CRM inválida.');
    });
  });

  it('é aberta pelo link da tela Entrar', async () => {
    renderizarApp('/entrar');
    await userEvent.setup().click(screen.getByRole('link', { name: 'Solicitar cadastro de médico' }));
    expect(screen.getByRole('heading', { name: 'Solicitar cadastro de médico' })).toBeInTheDocument();
  });

  it('quem já entrou vai para a sua tela', () => {
    comSessao(MEDICO);
    simularFetch(json(200, []));
    renderizarApp('/cadastro');
    expect(screen.getByRole('heading', { name: 'Pacientes', level: 1 })).toBeInTheDocument();
  });
});
