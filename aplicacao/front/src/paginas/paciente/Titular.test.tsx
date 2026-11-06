import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { detalhe, ID_PACIENTE, sessao, tcle } from '../../testes/pacientes';
import { ADMIN, comSessao, json, renderizarApp, simularApi, simularFetch } from '../../testes/utilitarios';

const ROTA = `/pacientes/${ID_PACIENTE}`;
const API = `/api/pacientes/${ID_PACIENTE}`;
const COMPLETA = sessao({
  id: 'antiga',
  status: 'COMPLETA',
  concluidaEm: '2026-10-01T13:20:00Z',
  analiseId: 'analise-1',
  analiseStatus: 'CONCLUIDA',
  analiseConcluidaEm: '2026-10-01T13:21:00Z',
  versaoModelo: 'v1_sintetico',
  origemDadosModelo: 'SINTETICO',
});

function corpo(fetchMock: ReturnType<typeof simularFetch>, chamada: number): unknown {
  return JSON.parse(fetchMock.mock.calls[chamada]![1]!.body as string);
}

describe('Paciente visto pelo administrador (ADR-0010) e pedidos do titular (UC12)', () => {
  it('mostra os dados sem as ações clínicas e oferece os pedidos do titular', async () => {
    comSessao(ADMIN);
    simularFetch(json(200, detalhe({ consentimentos: [tcle()], sessoes: [sessao(), COMPLETA] }, { podeColetar: true })));
    renderizarApp(ROTA);

    expect(await screen.findByRole('heading', { name: 'Criança Sintética', level: 1 })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Coleta das digitais' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Atualizar situação diagnóstica' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Registrar consentimento' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Revogar TCLE/ })).toBeInTheDocument();
    expect(screen.getByText(/O médico responsável registra o resultado/)).toBeInTheDocument();
    const coletas = screen.getByRole('region', { name: 'Tabela de coletas' });
    expect(within(coletas).queryByRole('link', { name: /Continuar coleta/ })).not.toBeInTheDocument();
    expect(within(coletas).getByRole('link', { name: /Ver o resultado/ })).toHaveAttribute('href', '/analises/analise-1');
    const titular = screen.getByRole('region', { name: 'Pedidos do titular (LGPD)' });
    expect(within(titular).getByRole('button', { name: 'Exportar dados' })).toBeInTheDocument();
  });

  it('o médico não vê os pedidos do titular', async () => {
    comSessao();
    simularFetch(json(200, detalhe()));
    renderizarApp(ROTA);
    await screen.findByRole('heading', { name: 'Criança Sintética', level: 1 });
    expect(screen.queryByRole('region', { name: 'Pedidos do titular (LGPD)' })).not.toBeInTheDocument();
  });

  it('exporta os dados como arquivo', async () => {
    comSessao(ADMIN);
    const zip = new Response('PK', {
      status: 200,
      headers: { 'Content-Type': 'application/zip', 'Content-Disposition': 'attachment; filename=paciente_c0de_20261105.zip' },
    });
    const fetchMock = simularApi({ [`GET ${API}`]: json(200, detalhe()), [`GET ${API}/exportacao`]: zip });
    const clique = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    renderizarApp(ROTA);
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Exportar dados' }));

    expect(await screen.findByText(/Arquivo paciente_c0de_20261105\.zip baixado/)).toBeInTheDocument();
    expect(clique).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls.some(([url]) => url === `${API}/exportacao`)).toBe(true);
  });

  it('corrige a identificação com as regras do cadastro', async () => {
    comSessao(ADMIN);
    const fetchMock = simularFetch(
      json(200, detalhe()),
      new Response(null, { status: 204 }),
      json(200, detalhe({}, { nome: 'Nome Corrigido' })),
    );
    renderizarApp(ROTA);
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Corrigir identificação' }));
    const formulario = screen.getByRole('form', { name: 'Corrigir identificação' });
    expect(within(formulario).getByLabelText('Nome')).toHaveFocus();
    await usuario.clear(within(formulario).getByLabelText('Contato do responsável'));
    await usuario.click(within(formulario).getByRole('button', { name: 'Salvar correção' }));
    expect(within(formulario).getByText('Informe um contato do responsável.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await usuario.clear(within(formulario).getByLabelText('Nome'));
    await usuario.type(within(formulario).getByLabelText('Nome'), 'Nome Corrigido');
    await usuario.type(within(formulario).getByLabelText('Contato do responsável'), '(00) 91111-1111');
    await usuario.click(within(formulario).getByRole('button', { name: 'Salvar correção' }));

    expect(await screen.findByText('Identificação corrigida.')).toBeInTheDocument();
    expect(fetchMock.mock.calls[1]![0]).toBe(`${API}/identificacao`);
    expect(fetchMock.mock.calls[1]![1]!.method).toBe('PUT');
    expect(corpo(fetchMock, 1)).toEqual({
      nome: 'Nome Corrigido',
      dataNascimento: '2022-03-14',
      responsavel: { nome: 'Responsável Sintético', parentesco: 'MAE', contato: '(00) 91111-1111' },
    });
  });

  it('só elimina com a confirmação digitada', async () => {
    comSessao(ADMIN);
    const fetchMock = simularFetch(
      json(200, detalhe()),
      json(200, { imagensApagadas: 10 }),
      json(200, detalhe({ responsavel: null }, { nome: null, dataNascimento: null, anonimizado: true })),
    );
    renderizarApp(ROTA);
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Eliminar dados' }));
    const formulario = screen.getByRole('form', { name: 'Eliminar dados do paciente' });
    expect(within(formulario).getByText(/Não dá para desfazer/)).toBeInTheDocument();
    await usuario.type(within(formulario).getByLabelText('Digite ELIMINAR para confirmar'), 'sim');
    await usuario.click(within(formulario).getByRole('button', { name: 'Eliminar os dados do paciente' }));
    expect(within(formulario).getByText('Digite ELIMINAR para confirmar.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await usuario.clear(within(formulario).getByLabelText('Digite ELIMINAR para confirmar'));
    await usuario.type(within(formulario).getByLabelText('Digite ELIMINAR para confirmar'), 'ELIMINAR');
    await usuario.click(within(formulario).getByRole('button', { name: 'Eliminar os dados do paciente' }));

    expect(await screen.findByRole('heading', { name: 'Dados eliminados a pedido do titular', level: 1 })).toBeInTheDocument();
    expect(corpo(fetchMock, 1)).toEqual({ confirmacao: 'ELIMINAR' });
    expect(screen.queryByRole('region', { name: 'Pedidos do titular (LGPD)' })).not.toBeInTheDocument();
  });

  it('a lista do administrador não oferece cadastrar paciente', async () => {
    comSessao(ADMIN);
    simularFetch(json(200, []));
    renderizarApp('/pacientes');
    expect(await screen.findByText('Nenhum paciente cadastrado pelos médicos ainda.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Cadastrar/ })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Pacientes' })).toHaveAttribute('aria-current', 'page');
  });
});
