import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { ModeloIA, PainelModelos, ServicoIA } from '../api/modelos';
import { ADMIN, comSessao, json, renderizarApp, simularFetch } from '../testes/utilitarios';

/** Versões sintéticas para os testes — nenhum modelo real (CLAUDE.md, regra 4). */
const ATIVO: ModeloIA = {
  id: 'm1',
  versao: '2026-11-04_padroes_sintetico_v1',
  arquitetura: 'CNN 3 blocos (32-128 filtros)',
  origemDados: 'SINTETICO',
  escoreValidado: false,
  hashSha256: 'a'.repeat(64),
  metricas: { acuracia: 0.986, acuraciaBalanceada: 0.9861, nTeste: 570, auc: 1 },
  treinadoEm: '2026-11-04',
  ativo: true,
  promovidoEm: '2026-11-04T22:40:00Z',
  analises: 6,
};

const NOVA = '2026-11-05_padroes_sintetico_v2';

function servico(extra: Partial<ServicoIA> = {}): ServicoIA {
  return {
    disponivel: true,
    versaoModelo: NOVA,
    origemDados: 'SINTETICO',
    sha256: 'b'.repeat(64),
    pastaOrigem: 'DEMO',
    motivo: null,
    registrado: false,
    ativo: false,
    hashConfere: true,
    ...extra,
  };
}

function painel(s: ServicoIA, modelos: ModeloIA[] = [ATIVO]): PainelModelos {
  return { servico: s, modelos };
}

function abrir(...respostas: Response[]) {
  comSessao(ADMIN);
  const fetchMock = simularFetch(...respostas);
  renderizarApp('/admin/modelos');
  return { fetchMock, usuario: userEvent.setup() };
}

const arquivo = (nome: string) => new File(['{}'], nome, { type: 'application/json' });

describe('Modelos de IA (UC10)', () => {
  it('mostra o modelo do serviço, a situação e as versões registradas', async () => {
    abrir(json(200, painel(servico({ versaoModelo: ATIVO.versao, registrado: true, ativo: true, sha256: ATIVO.hashSha256 }))));

    expect(await screen.findByText('Registrado e ativo. As análises usam esta versão.')).toBeInTheDocument();
    expect(document.title).toBe('Modelos de IA | Dermatoglifia TEA');
    expect(screen.getByRole('link', { name: 'Modelos de IA' })).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('form')).not.toBeInTheDocument();
    const linha = screen.getByRole('row', { name: new RegExp(ATIVO.versao) });
    expect(linha).toHaveTextContent('Sintéticos (demonstração)');
    expect(linha).toHaveTextContent('0,986');
    expect(linha).toHaveTextContent('570 imagens');
    expect(linha).toHaveTextContent(/Ativa desde/);
  });

  it('promove a versão carregada enviando config.json e metricas.json', async () => {
    const promovido = { ...ATIVO, id: 'm2', versao: NOVA, hashSha256: 'b'.repeat(64), promovidoEm: '2026-11-05T23:00:00Z' };
    const { fetchMock, usuario } = abrir(
      json(200, painel(servico())),
      json(200, promovido),
      json(200, painel(servico({ registrado: true, ativo: true }), [promovido, { ...ATIVO, ativo: false }])),
    );

    expect(await screen.findByText(/Ainda não registrado/)).toBeInTheDocument();
    const formulario = screen.getByRole('form', { name: `Promover a versão ${NOVA}` });
    expect(within(formulario).getByText(/treinado só com dados sintéticos/)).toBeInTheDocument();

    await usuario.click(within(formulario).getByRole('button', { name: `Promover ${NOVA}` }));
    expect(within(formulario).getByText('Escolha o config.json.')).toBeInTheDocument();
    expect(within(formulario).getByText('Escolha o metricas.json.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await usuario.upload(within(formulario).getByLabelText('config.json'), arquivo('config.json'));
    await usuario.upload(within(formulario).getByLabelText('metricas.json'), arquivo('metricas.json'));
    await usuario.click(within(formulario).getByRole('button', { name: `Promover ${NOVA}` }));

    expect(await screen.findByText(`Versão ${NOVA} promovida. As próximas análises usam este modelo.`)).toBeInTheDocument();
    expect(fetchMock.mock.calls[1]![0]).toBe('/api/modelos/promocao');
    const corpo = fetchMock.mock.calls[1]![1]!.body as FormData;
    expect((corpo.get('config') as File).name).toBe('config.json');
    expect((corpo.get('metricas') as File).name).toBe('metricas.json');
    expect(await screen.findByText('Registrado e ativo. As análises usam esta versão.')).toBeInTheDocument();
  });

  it('mostra o motivo quando a API recusa a promoção', async () => {
    const { usuario } = abrir(
      json(200, painel(servico())),
      json(422, { title: 'O Serviço de IA está com a versão "x", não "y".', status: 422 }),
    );

    const formulario = await screen.findByRole('form', { name: `Promover a versão ${NOVA}` });
    await usuario.upload(within(formulario).getByLabelText('config.json'), arquivo('config.json'));
    await usuario.upload(within(formulario).getByLabelText('metricas.json'), arquivo('metricas.json'));
    await usuario.click(within(formulario).getByRole('button', { name: `Promover ${NOVA}` }));

    expect(await within(formulario).findByText('O Serviço de IA está com a versão "x", não "y".')).toBeInTheDocument();
  });

  it('não oferece promoção quando o hash não confere', async () => {
    abrir(json(200, painel(servico({ registrado: true, hashConfere: false }))));
    expect(await screen.findByText(/já foi registrada com outro arquivo/)).toBeInTheDocument();
    expect(screen.queryByRole('form')).not.toBeInTheDocument();
  });

  it('explica quando o serviço está fora do ar', async () => {
    abrir(json(200, painel({ ...servico(), disponivel: false, versaoModelo: null, motivo: 'Serviço de IA indisponível.' })));
    expect(await screen.findByText(/Serviço de IA indisponível\. Enquanto isso, as análises ficam com erro/)).toBeInTheDocument();
    expect(screen.getByRole('row', { name: new RegExp(ATIVO.versao) })).toBeInTheDocument();
  });
});
