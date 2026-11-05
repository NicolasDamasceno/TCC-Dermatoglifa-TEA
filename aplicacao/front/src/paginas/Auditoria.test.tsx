import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { FiltrosAuditoria, PaginaAuditoria, RegistroAuditoria } from '../api/auditoria';
import { ADMIN, comSessao, json, renderizarApp } from '../testes/utilitarios';

/** Eventos sintéticos — nenhum dado real (CLAUDE.md, regra 4). */
const PACIENTE = '00000000-0000-0000-0000-00000000c0de';
const MEDICO = { id: '00000000-0000-0000-0000-000000000001', nome: 'Dra. Sintética', email: 'dra@exemplo.test', papel: 'MEDICO' as const };

function evento(extra: Partial<RegistroAuditoria> = {}): RegistroAuditoria {
  return {
    id: 10,
    ocorridoEm: '2026-11-05T22:00:00Z',
    usuarioId: MEDICO.id,
    usuario: MEDICO,
    acao: 'SITUACAO_DIAGNOSTICA_ATUALIZADA',
    entidade: 'paciente',
    entidadeId: PACIENTE,
    ip: '127.0.0.1',
    detalhes: { situacao_anterior: 'SUSPEITA', situacao_nova: 'CONTROLE' },
    ...extra,
  };
}

const FILTROS: FiltrosAuditoria = {
  acoes: ['LOGIN_OK', 'SITUACAO_DIAGNOSTICA_ATUALIZADA'],
  usuarios: [MEDICO, { id: 'adm', nome: 'Admin Sintético', email: 'admin@exemplo.test', papel: 'ADMIN' }],
};

function pagina(itens: RegistroAuditoria[], extra: Partial<PaginaAuditoria> = {}): PaginaAuditoria {
  return { itens, total: itens.length, pagina: 1, tamanho: 50, ...extra };
}

/** Fetch por caminho; guarda as consultas da trilha para conferir os parâmetros. */
function abrir(rota: string, ...paginas: PaginaAuditoria[]) {
  comSessao(ADMIN);
  const consultas: URLSearchParams[] = [];
  const fila = [...paginas];
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string) => {
      const [caminho, busca = ''] = url.split('?');
      if (caminho === '/api/auditoria/filtros') return Promise.resolve(json(200, FILTROS));
      if (caminho === '/api/auditoria') {
        consultas.push(new URLSearchParams(busca));
        const resposta = fila.length > 1 ? fila.shift()! : fila[0]!;
        return Promise.resolve(json(200, resposta));
      }
      return Promise.reject(new Error(`fetch inesperado: ${url}`));
    }),
  );
  renderizarApp(rota);
  return { consultas, usuario: userEvent.setup() };
}

describe('Trilha de auditoria (UC11)', () => {
  it('mostra os eventos com ação por extenso, código, registro e detalhes', async () => {
    abrir('/admin/auditoria', pagina([evento(), evento({ id: 9, acao: 'LOGIN_FALHA', usuarioId: null, usuario: null, entidade: 'usuario', entidadeId: null, detalhes: null })]));

    const tabela = await screen.findByRole('table', { name: /2 eventos; página 1 de 1/ });
    expect(document.title).toBe('Trilha de auditoria | Dermatoglifia TEA');
    expect(screen.getByRole('link', { name: 'Auditoria' })).toHaveAttribute('aria-current', 'page');
    const linha = within(tabela).getByRole('row', { name: /Situação diagnóstica atualizada/ });
    expect(linha).toHaveTextContent('Dra. Sintética');
    expect(linha).toHaveTextContent('SITUACAO_DIAGNOSTICA_ATUALIZADA');
    expect(linha).toHaveTextContent(PACIENTE);
    expect(linha).toHaveTextContent('situacao_nova: CONTROLE');
    expect(within(tabela).getByRole('row', { name: /credenciais inválidas/ })).toHaveTextContent('sem usuário identificado');
  });

  it('filtra pela trilha de um paciente a partir de uma linha', async () => {
    const { consultas, usuario } = abrir('/admin/auditoria', pagina([evento()]), pagina([evento()]));

    await usuario.click(await screen.findByRole('link', { name: `Ver a trilha deste paciente (${PACIENTE})` }));

    await vi.waitFor(() => {
      expect(consultas.at(-1)?.get('pacienteId')).toBe(PACIENTE);
    });
    expect(screen.getByLabelText('Paciente')).toHaveValue(PACIENTE);
  });

  it('envia usuário, ação e período; o fim do período inclui o dia inteiro', async () => {
    const { consultas, usuario } = abrir('/admin/auditoria', pagina([evento()]));
    const formulario = await screen.findByRole('form', { name: 'Filtrar a trilha' });
    await within(formulario).findByRole('option', { name: /Dra. Sintética/ });

    await usuario.selectOptions(within(formulario).getByLabelText('Usuário'), MEDICO.id);
    await usuario.selectOptions(within(formulario).getByLabelText('Ação'), 'SITUACAO_DIAGNOSTICA_ATUALIZADA');
    await usuario.type(within(formulario).getByLabelText('De'), '2026-11-01');
    await usuario.type(within(formulario).getByLabelText('Até'), '2026-11-05');
    await usuario.click(within(formulario).getByRole('button', { name: 'Filtrar' }));

    await vi.waitFor(() => {
      expect(consultas.at(-1)?.get('usuarioId')).toBe(MEDICO.id);
    });
    const c = consultas.at(-1)!;
    expect(c.get('acao')).toBe('SITUACAO_DIAGNOSTICA_ATUALIZADA');
    expect(c.get('de')).toBe(new Date(2026, 10, 1).toISOString());
    expect(c.get('ate')).toBe(new Date(2026, 10, 6).toISOString());
  });

  it('recusa identificador de paciente inválido e período invertido sem consultar', async () => {
    const { consultas, usuario } = abrir('/admin/auditoria', pagina([evento()]));
    const formulario = await screen.findByRole('form', { name: 'Filtrar a trilha' });
    await screen.findByRole('table');
    const antes = consultas.length;

    await usuario.type(within(formulario).getByLabelText('Paciente'), 'Maria');
    await usuario.type(within(formulario).getByLabelText('De'), '2026-11-05');
    await usuario.type(within(formulario).getByLabelText('Até'), '2026-11-01');
    await usuario.click(within(formulario).getByRole('button', { name: 'Filtrar' }));

    expect(within(formulario).getByText(/Use o identificador do paciente/)).toBeInTheDocument();
    expect(within(formulario).getByText('O fim do período deve ser depois do início.')).toBeInTheDocument();
    expect(consultas.length).toBe(antes);
  });

  it('pagina para eventos mais antigos e avisa quando não há eventos', async () => {
    const { consultas, usuario } = abrir(
      '/admin/auditoria?acao=LOGIN_OK',
      pagina([evento()], { total: 120, tamanho: 50 }),
      pagina([], { total: 0, pagina: 2 }),
    );

    expect(await screen.findByRole('table', { name: /120 eventos; página 1 de 3/ })).toBeInTheDocument();
    await usuario.click(screen.getByRole('link', { name: 'Eventos mais antigos' }));

    expect(await screen.findByText('Nenhum evento com esses filtros.')).toBeInTheDocument();
    expect(consultas.at(-1)?.get('pagina')).toBe('2');
    expect(consultas.at(-1)?.get('acao')).toBe('LOGIN_OK');
  });
});
