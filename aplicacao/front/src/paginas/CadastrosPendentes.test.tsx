import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { SolicitacaoPendente } from '../api/medicos';
import { ADMIN, comSessao, json, renderizarApp, simularFetch } from '../testes/utilitarios';

const PEDIDO: SolicitacaoPendente = {
  id: '00000000-0000-0000-0000-0000000000aa',
  nome: 'Dr. Pedido Sintético',
  email: 'pedido@exemplo.test',
  crm: '654321',
  ufCrm: 'RJ',
  especialidade: 'Psiquiatria infantil',
  solicitadoEm: '2026-11-03T14:30:00Z',
};

const OUTRO: SolicitacaoPendente = { ...PEDIDO, id: '00000000-0000-0000-0000-0000000000bb', nome: 'Dra. Outra Sintética' };

function abrir(...respostas: Response[]) {
  comSessao(ADMIN);
  const fetchMock = simularFetch(...respostas);
  renderizarApp('/admin/solicitacoes');
  return fetchMock;
}

describe('Cadastros pendentes (UC03)', () => {
  it('lista os pedidos com CRM/UF e especialidade', async () => {
    abrir(json(200, [PEDIDO, OUTRO]));

    const tabela = await screen.findByRole('table', { name: /2 pedidos aguardando análise/ });
    const linha = within(tabela).getByRole('row', { name: /Dr\. Pedido Sintético/ });
    expect(linha).toHaveTextContent('654321/RJ');
    expect(linha).toHaveTextContent('Psiquiatria infantil');
    expect(linha).toHaveTextContent('pedido@exemplo.test');
  });

  it('diz quando não há pedidos', async () => {
    abrir(json(200, []));
    expect(await screen.findByText('Nenhum pedido aguardando análise.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('oferece tentar de novo quando a lista falha', async () => {
    abrir(json(500, {}), json(200, [PEDIDO]));

    expect(await screen.findByRole('alert')).toHaveTextContent('O servidor teve um problema');
    await userEvent.setup().click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByRole('table')).toBeInTheDocument();
  });

  it('aprova com o vínculo ao protocolo e tira o pedido da lista', async () => {
    const fetchMock = abrir(json(200, [PEDIDO, OUTRO]), new Response(null, { status: 204 }), json(200, [OUTRO]));
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Analisar pedido de Dr. Pedido Sintético' }));
    expect(screen.getByRole('button', { name: 'Fechar análise de Dr. Pedido Sintético' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );

    const painel = screen.getByRole('form', { name: 'Decisão sobre Dr. Pedido Sintético' });
    expect(within(painel).getByRole('radio', { name: 'Aprovar' })).toBeChecked();
    await usuario.type(within(painel).getByLabelText('Vínculo com o protocolo'), 'Protocolo sintético, pesquisador');
    await usuario.click(within(painel).getByRole('button', { name: 'Aprovar cadastro' }));

    const aviso = await screen.findByText('Cadastro de Dr. Pedido Sintético aprovado.');
    expect(aviso.closest('[tabindex="-1"]')).toHaveFocus();
    expect(await screen.findByRole('table', { name: /1 pedido aguardando/ })).toBeInTheDocument();
    expect(screen.queryByText('Dr. Pedido Sintético')).not.toBeInTheDocument();

    const [url, init] = fetchMock.mock.calls[1]!;
    expect(url).toBe(`/api/medicos/${PEDIDO.id}/aprovacao`);
    expect(JSON.parse(init!.body as string)).toEqual({ vinculoProtocolo: 'Protocolo sintético, pesquisador' });
  });

  it('recusa exigindo o motivo', async () => {
    const fetchMock = abrir(json(200, [PEDIDO]), new Response(null, { status: 204 }), json(200, []));
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Analisar pedido de Dr. Pedido Sintético' }));
    await usuario.click(screen.getByRole('radio', { name: 'Recusar' }));
    await usuario.click(screen.getByRole('button', { name: 'Recusar cadastro' }));

    expect(screen.getByLabelText('Motivo da recusa')).toHaveAccessibleDescription(
      'Fica registrado no cadastro do médico. Informe o motivo da recusa.',
    );
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await usuario.type(screen.getByLabelText('Motivo da recusa'), 'Não integra a equipe do protocolo.');
    await usuario.click(screen.getByRole('button', { name: 'Recusar cadastro' }));

    expect(await screen.findByText('Cadastro de Dr. Pedido Sintético recusado.')).toBeInTheDocument();
    expect(await screen.findByText('Nenhum pedido aguardando análise.')).toBeInTheDocument();
    const [url, init] = fetchMock.mock.calls[1]!;
    expect(url).toBe(`/api/medicos/${PEDIDO.id}/recusa`);
    expect(JSON.parse(init!.body as string)).toEqual({ motivo: 'Não integra a equipe do protocolo.' });
  });

  it('avisa e recarrega quando outro administrador já decidiu', async () => {
    const fetchMock = abrir(
      json(200, [PEDIDO]),
      json(422, { title: 'Este cadastro já foi analisado.', status: 422 }),
      json(200, []),
    );
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Analisar pedido de Dr. Pedido Sintético' }));
    await usuario.type(screen.getByLabelText('Vínculo com o protocolo'), 'Protocolo sintético');
    await usuario.click(screen.getByRole('button', { name: 'Aprovar cadastro' }));

    expect(await screen.findByText('Nenhum pedido aguardando análise.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it('mostra o erro da API no painel enquanto o pedido continua na lista', async () => {
    abrir(json(200, [PEDIDO]), json(500, {}));
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Analisar pedido de Dr. Pedido Sintético' }));
    await usuario.type(screen.getByLabelText('Vínculo com o protocolo'), 'Protocolo sintético');
    await usuario.click(screen.getByRole('button', { name: 'Aprovar cadastro' }));

    const painel = screen.getByRole('form', { name: 'Decisão sobre Dr. Pedido Sintético' });
    expect(await within(painel).findByRole('alert')).toHaveTextContent('O servidor teve um problema');
    expect(within(painel).getByLabelText('Vínculo com o protocolo')).toHaveValue('Protocolo sintético');
  });
});
