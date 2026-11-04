import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { SituacaoDedo } from '../../api/coletas';
import { analise } from '../../testes/analises';
import { arquivo, ID_SESSAO, sessaoColeta, TODOS_COLETADOS } from '../../testes/coletas';
import { detalhe, ID_PACIENTE } from '../../testes/pacientes';
import { comSessao, json, renderizarApp, simularApi } from '../../testes/utilitarios';
import { proximoPendente } from './ordem';

const BASE = `/api/sessoes/${ID_SESSAO}`;

function abrir(rotas: Parameters<typeof simularApi>[0]) {
  comSessao();
  const fetchMock = simularApi({ [`GET /api/pacientes/${ID_PACIENTE}`]: json(200, detalhe()), ...rotas });
  renderizarApp(`/sessoes/${ID_SESSAO}`);
  return { fetchMock, usuario: userEvent.setup({ applyAccept: false }) };
}

function quadro(nome: RegExp) {
  return screen.getByRole('button', { name: nome });
}

/** 1º dedo coletado, o resto pendente. */
const PRIMEIRO_COLETADO: SituacaoDedo[] = ['COLETADO'];

describe('Coleta (UC06)', () => {
  it('mostra a ficha com os 10 dedos e começa pelo polegar direito', async () => {
    abrir({ [`GET ${BASE}`]: json(200, sessaoColeta()) });

    const ficha = await screen.findByRole('table', { name: 'Ficha decadactilar' });
    expect(within(ficha).getAllByRole('button')).toHaveLength(10);
    expect(quadro(/^Direita, Polegar: pendente/)).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('heading', { name: 'Polegar direito', level: 2 })).toBeInTheDocument();
    expect(screen.getByText('0 de 10')).toBeInTheDocument();
    expect(await screen.findByText('Criança Sintética', { selector: 'dd' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Concluir coleta' })).toBeDisabled();
  });

  it('recusa formato com perda antes de enviar', async () => {
    const { fetchMock, usuario } = abrir({ [`GET ${BASE}`]: json(200, sessaoColeta()) });

    await usuario.upload(await screen.findByLabelText('Imagem do polegar direito'), arquivo('foto.jpg', 'image/jpeg'));

    expect(screen.getByLabelText('Imagem do polegar direito')).toHaveAccessibleDescription(
      'PNG, BMP ou TIFF sem perda, com pelo menos 500 ppi e até 5 MB. Formato não aceito. Envie PNG, BMP ou TIFF (sem perda; JPEG não é aceito).',
    );
    expect(screen.queryByRole('button', { name: /^Enviar/ })).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(0);
  });

  it('recusa arquivo acima de 5 MB', async () => {
    const { usuario } = abrir({ [`GET ${BASE}`]: json(200, sessaoColeta()) });
    await usuario.upload(await screen.findByLabelText('Imagem do polegar direito'), arquivo('grande.png', 'image/png', 5 * 1024 * 1024 + 1));
    expect(screen.getByText('Arquivo acima de 5 MB.')).toBeInTheDocument();
  });

  it('pré-visualiza, envia só após confirmar e passa ao próximo dedo', async () => {
    const { fetchMock, usuario } = abrir({
      [`GET ${BASE}`]: json(200, sessaoColeta()),
      [`PUT ${BASE}/digitais/DIREITA/POLEGAR`]: json(200, sessaoColeta({}, PRIMEIRO_COLETADO)),
    });
    const imagem = arquivo('polegar-direito.png');

    await usuario.upload(await screen.findByLabelText('Imagem do polegar direito'), imagem);

    const previa = screen.getByRole('img', { name: 'Pré-visualização da imagem escolhida para o polegar direito' });
    expect(previa.getAttribute('src')).toMatch(/^blob:/);
    expect(screen.getByText('Confira: esta é a digital do polegar direito?')).toBeInTheDocument();
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(0);

    await usuario.click(screen.getByRole('button', { name: 'Enviar polegar direito' }));

    const proximo = await screen.findByRole('heading', { name: 'Indicador direito', level: 2 });
    expect(proximo).toHaveFocus();
    expect(quadro(/^Direita, Polegar: coletado/)).toHaveAttribute('aria-pressed', 'false');
    expect(quadro(/^Direita, Indicador: pendente/)).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('status', { name: '' })).toHaveTextContent('Polegar direito enviado. Próximo: indicador direito.');
    expect(screen.getByText('1 de 10')).toBeInTheDocument();

    const envio = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')!;
    const corpo = envio[1]!.body as FormData;
    const enviado = corpo.get('imagem') as File;
    expect([enviado.name, enviado.size]).toEqual([imagem.name, imagem.size]);
  });

  it('mostra a recusa da API e mantém o dedo pendente', async () => {
    const { usuario } = abrir({
      [`GET ${BASE}`]: json(200, sessaoColeta()),
      [`PUT ${BASE}/digitais/DIREITA/POLEGAR`]: json(422, { title: 'Resolução abaixo de 500 ppi (encontrado: 300 ppi).' }),
    });

    await usuario.upload(await screen.findByLabelText('Imagem do polegar direito'), arquivo());
    await usuario.click(screen.getByRole('button', { name: 'Enviar polegar direito' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Resolução abaixo de 500 ppi');
    expect(quadro(/^Direita, Polegar: pendente/)).toBeInTheDocument();
  });

  it('registra dedo não coletável com motivo', async () => {
    const situacoes: SituacaoDedo[] = [...Array<SituacaoDedo>(9).fill('PENDENTE'), 'NAO_COLETAVEL'];
    const { fetchMock, usuario } = abrir({
      [`GET ${BASE}`]: json(200, sessaoColeta()),
      [`POST ${BASE}/digitais/ESQUERDA/MINIMO/nao-coletavel`]: json(200, sessaoColeta({}, situacoes)),
    });

    await usuario.click(await screen.findByRole('button', { name: /^Esquerda, Mínimo/ }));
    expect(screen.getByRole('heading', { name: 'Mínimo esquerdo', level: 2 })).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Este dedo não pode ser coletado' }));
    await usuario.click(screen.getByRole('button', { name: 'Registrar como não coletável' }));
    expect(screen.getByLabelText('Motivo')).toHaveAccessibleDescription(/Informe o motivo\.$/);

    await usuario.type(screen.getByLabelText('Motivo'), 'Lesão (sintético)');
    await usuario.click(screen.getByRole('button', { name: 'Registrar como não coletável' }));

    expect(await screen.findByRole('button', { name: /^Esquerda, Mínimo: não coletável/ })).toBeInTheDocument();
    const chamada = fetchMock.mock.calls.find(([url]) => url.endsWith('/nao-coletavel'))!;
    expect(JSON.parse(chamada[1]!.body as string)).toEqual({ motivo: 'Lesão (sintético)' });
    // Volta ao primeiro pendente da ficha.
    expect(await screen.findByRole('heading', { name: 'Polegar direito', level: 2 })).toHaveFocus();
  });

  it('conclui com os 10 dedos e abre o resultado da análise', async () => {
    const { usuario } = abrir({
      [`GET ${BASE}`]: json(200, sessaoColeta({}, TODOS_COLETADOS)),
      [`POST ${BASE}/conclusao`]: json(
        200,
        sessaoColeta({ status: 'COMPLETA', analiseId: 'analise-1', analiseStatus: 'CONCLUIDA' }, TODOS_COLETADOS),
      ),
      'GET /api/analises/analise-1': json(200, analise({ id: 'analise-1' })),
    });

    expect(await screen.findByText(/Os 10 dedos foram coletados/)).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Concluir coleta' }));

    expect(await screen.findByRole('heading', { name: 'Resultado da análise', level: 1 })).toBeInTheDocument();
  });

  it('conclui incompleta sem análise e deixa a ficha só para leitura', async () => {
    const situacoes: SituacaoDedo[] = [...Array<SituacaoDedo>(9).fill('COLETADO'), 'NAO_COLETAVEL'];
    const { usuario } = abrir({
      [`GET ${BASE}`]: json(200, sessaoColeta({}, situacoes)),
      [`POST ${BASE}/conclusao`]: json(
        200,
        sessaoColeta({ status: 'INCOMPLETA', concluidaEm: '2026-11-04T13:30:00Z' }, situacoes),
      ),
    });

    expect(await screen.findByText(/termina incompleta/)).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Concluir coleta' }));

    expect(await screen.findByText(/^Coleta incompleta em/)).toBeInTheDocument();
    expect(screen.getByText(/Coletas incompletas não são analisadas/)).toBeInTheDocument();
    const ficha = screen.getByRole('table', { name: 'Ficha decadactilar' });
    expect(within(ficha).queryAllByRole('button')).toHaveLength(0);
    expect(screen.queryByRole('button', { name: 'Concluir coleta' })).not.toBeInTheDocument();
  });

  it('cancela só depois de confirmar', async () => {
    const { fetchMock, usuario } = abrir({
      [`GET ${BASE}`]: json(200, sessaoColeta()),
      [`POST ${BASE}/cancelamento`]: json(200, sessaoColeta({ status: 'CANCELADA', concluidaEm: '2026-11-04T13:30:00Z' })),
    });

    await usuario.click(await screen.findByRole('button', { name: 'Cancelar coleta' }));
    await usuario.click(screen.getByRole('button', { name: 'Continuar coletando' }));
    expect(fetchMock.mock.calls.filter(([url]) => url.endsWith('/cancelamento'))).toHaveLength(0);

    await usuario.click(screen.getByRole('button', { name: 'Cancelar coleta' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar cancelamento' }));

    expect(await screen.findByText(/^Coleta cancelada em/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Voltar para o paciente' })).toHaveAttribute('href', `/pacientes/${ID_PACIENTE}`);
  });

  it('coleta completa leva ao resultado', async () => {
    abrir({
      [`GET ${BASE}`]: json(
        200,
        sessaoColeta(
          { status: 'COMPLETA', concluidaEm: '2026-11-04T13:30:00Z', analiseId: 'analise-1', analiseStatus: 'ERRO' },
          TODOS_COLETADOS,
        ),
      ),
    });

    expect(await screen.findByRole('link', { name: 'Ver o resultado da análise' })).toHaveAttribute(
      'href',
      '/analises/analise-1',
    );
  });

  it('avisa quando a coleta não existe ou é de outro médico', async () => {
    abrir({ [`GET ${BASE}`]: json(404, { title: 'Sessão não encontrada.' }) });
    expect(await screen.findByRole('heading', { name: 'Coleta não encontrada' })).toBeInTheDocument();
  });

  it('TIFF é aceito, sem pré-visualização, e pede conferência pelo nome', async () => {
    const { usuario } = abrir({ [`GET ${BASE}`]: json(200, sessaoColeta()) });
    await usuario.upload(await screen.findByLabelText('Imagem do polegar direito'), arquivo('polegar.tif', 'image/tiff'));

    await waitFor(() => {
      expect(screen.getByText('O navegador não mostra arquivos TIFF. Confira pelo nome do arquivo.')).toBeInTheDocument();
    });
    expect(screen.getByText('polegar.tif')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar polegar direito' })).toBeInTheDocument();
  });
});

describe('proximoPendente', () => {
  const dedos = sessaoColeta({}, ['COLETADO', 'PENDENTE', 'COLETADO', 'PENDENTE']).dedos;

  it('segue a ordem da ficha a partir do dedo atual e volta ao início', () => {
    expect(proximoPendente(dedos)?.dedo).toBe('INDICADOR');
    expect(proximoPendente(dedos, 'DIREITA-INDICADOR')?.dedo).toBe('ANELAR');
    const soInicio = sessaoColeta({}, ['PENDENTE', ...Array<SituacaoDedo>(9).fill('COLETADO')]).dedos;
    expect(proximoPendente(soInicio, 'ESQUERDA-MEDIO')?.dedo).toBe('POLEGAR');
    expect(proximoPendente(sessaoColeta({}, TODOS_COLETADOS).dedos)).toBeUndefined();
  });
});
