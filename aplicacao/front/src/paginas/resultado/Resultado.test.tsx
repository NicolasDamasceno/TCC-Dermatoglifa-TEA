import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { analise, ID_ANALISE } from '../../testes/analises';
import { detalhe, ID_PACIENTE } from '../../testes/pacientes';
import { comSessao, json, renderizarApp, simularApi } from '../../testes/utilitarios';

const BASE = `/api/analises/${ID_ANALISE}`;

function abrir(rotas: Parameters<typeof simularApi>[0]) {
  comSessao();
  const fetchMock = simularApi({ [`GET /api/pacientes/${ID_PACIENTE}`]: json(200, detalhe()), ...rotas });
  renderizarApp(`/analises/${ID_ANALISE}`);
  return { fetchMock, usuario: userEvent.setup() };
}

function corpoDe(fetchMock: ReturnType<typeof simularApi>, metodo: string): unknown {
  const chamada = fetchMock.mock.calls.find(([, init]) => init?.method === metodo)!;
  return JSON.parse(chamada[1]!.body as string);
}

describe('Resultado da análise (UC08)', () => {
  it('segue a ordem de leitura: aviso, alertas, ficha, indicadores, modelo e registro', async () => {
    abrir({ [`GET ${BASE}`]: json(200, analise()) });

    const aviso = await screen.findByRole('region', { name: 'Não é diagnóstico' });
    expect(aviso).toHaveTextContent('Protótipo de pesquisa (aviso sintético de teste).');
    expect(aviso).toHaveTextContent('Versão do aviso: v1');

    const titulos = screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent);
    expect(titulos).toEqual([
      'Não é diagnóstico',
      'Leia também',
      'Padrão de cada dedo',
      'Indicadores',
      'Modelo que respondeu',
      'Registro do médico',
    ]);
    const alertas = within(screen.getByRole('region', { name: 'Leia também' })).getAllByRole('listitem');
    expect(alertas.map((a) => a.textContent)).toEqual([
      'Sem ponto de corte validado (teste).',
      'Escore não validado (teste).',
      'Resultado de demonstração (teste).',
    ]);
  });

  it('mostra o padrão e a confiança de cada dedo na ficha', async () => {
    abrir({ [`GET ${BASE}`]: json(200, analise()) });

    const ficha = await screen.findByRole('table', { name: 'Padrão previsto e confiança por dedo' });
    const direita = within(ficha).getByRole('row', { name: /^Direita/ });
    expect(within(direita).getAllByRole('cell')[0]).toHaveTextContent('Verticiloconfiança 91%');
    expect(within(direita).getAllByRole('cell')[1]).toHaveTextContent('Alça ulnar');
    expect(within(ficha).getByRole('row', { name: /^Esquerda/ })).toHaveTextContent('Arco');
  });

  it('marca dedo com baixa qualidade de imagem', async () => {
    const base = analise();
    const digitais = base.digitais.map((d, i) => (i === 3 ? { ...d, alertaQualidade: true } : d));
    abrir({ [`GET ${BASE}`]: json(200, { ...base, digitais }) });

    const ficha = await screen.findByRole('table', { name: 'Padrão previsto e confiança por dedo' });
    const direita = within(ficha).getByRole('row', { name: /^Direita/ });
    expect(within(direita).getAllByRole('cell')[3]).toHaveTextContent('Baixa qualidade');
    expect(screen.getByText(/podem ter o padrão errado/)).toBeInTheDocument();
  });

  it('lista os indicadores com a escala e o que cada um conta, sem interpretar', async () => {
    abrir({ [`GET ${BASE}`]: json(200, analise()) });

    const tabela = await screen.findByRole('region', { name: 'Tabela de indicadores' });
    expect(within(tabela).getByRole('row', { name: /^Verticilos/ })).toHaveTextContent('2 de 10');
    const discordancia = within(tabela).getByRole('row', { name: /^Discordância bilateral(?! com)/ });
    expect(discordancia).toHaveTextContent('4 de 4');
    expect(discordancia).toHaveTextContent('polegar excluído, como em de Bruin et al. (2014)');
    expect(within(tabela).getByRole('row', { name: /^Discordância bilateral com polegar/ })).toHaveTextContent('4 de 5');
    expect(tabela.textContent).not.toMatch(/alto|baixo|elevad|normal/i);
  });

  it('não mostra escore enquanto o modelo de risco não está validado (RN10)', async () => {
    abrir({ [`GET ${BASE}`]: json(200, analise({ escoreRisco: 0.42 })) });

    await screen.findByRole('region', { name: 'Indicadores' });
    expect(screen.queryByRole('region', { name: 'Escore de risco' })).not.toBeInTheDocument();
    expect(screen.queryByText('0.42')).not.toBeInTheDocument();
  });

  it('mostra o escore só com modelo validado, sem classificar o paciente', async () => {
    const base = analise({ escoreRisco: 0.42 });
    abrir({ [`GET ${BASE}`]: json(200, { ...base, modelo: { ...base.modelo!, escoreValidado: true } }) });

    const escore = await screen.findByRole('region', { name: 'Escore de risco' });
    expect(escore).toHaveTextContent('0.42');
    expect(escore).toHaveTextContent('sem ponto de corte validado');
  });

  it('mostra versão, origem e métricas do modelo', async () => {
    abrir({ [`GET ${BASE}`]: json(200, analise()) });

    const modelo = await screen.findByRole('region', { name: 'Modelo que respondeu' });
    expect(modelo).toHaveTextContent('teste_sintetico_v1');
    expect(modelo).toHaveTextContent('Dados sintéticos');
    expect(modelo).toHaveTextContent('Treinado em01/11/2026');
    expect(modelo).toHaveTextContent('Acurácia balanceada0,88');
    expect(modelo).toHaveTextContent('Imagens no teste500');
    expect(modelo).toHaveTextContent('Não mede a capacidade de identificar TEA');
  });

  it('análise com erro oferece tentar de novo sem refazer a coleta', async () => {
    const { usuario } = abrir({
      [`GET ${BASE}`]: json(
        200,
        analise({ status: 'ERRO', mensagemErro: 'Serviço de IA indisponível (teste).', digitais: [], indicadores: null, modelo: null, concluidaEm: null }),
      ),
      [`POST ${BASE}/nova-tentativa`]: json(200, analise()),
    });

    const erro = await screen.findByRole('alert');
    expect(erro).toHaveTextContent('Serviço de IA indisponível (teste).');
    expect(screen.getByRole('region', { name: 'Não é diagnóstico' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Registro do médico' })).not.toBeInTheDocument();

    await usuario.click(within(erro).getByRole('button', { name: 'Tentar análise de novo' }));

    expect(await screen.findByRole('region', { name: 'Indicadores' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('avisa que a análise está em processamento', async () => {
    abrir({ [`GET ${BASE}`]: json(200, analise({ status: 'PROCESSANDO', digitais: [], indicadores: null, concluidaEm: null })) });
    expect(await screen.findByText('Análise em processamento.')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Indicadores' })).not.toBeInTheDocument();
  });

  it('salva a observação do médico', async () => {
    const { fetchMock, usuario } = abrir({
      [`GET ${BASE}`]: json(200, analise()),
      [`PATCH ${BASE}/observacao`]: json(200, analise({ observacaoMedico: 'Contexto clínico sintético.' })),
    });

    const salvar = await screen.findByRole('button', { name: 'Salvar observação' });
    expect(salvar).toBeDisabled();
    await usuario.type(screen.getByLabelText('Observação do médico'), ' Contexto clínico sintético. ');
    await usuario.click(salvar);

    expect(await screen.findByText('Observação salva.')).toBeInTheDocument();
    expect(corpoDe(fetchMock, 'PATCH')).toEqual({ observacao: 'Contexto clínico sintético.' });
    expect(screen.getByLabelText('Observação do médico')).toHaveValue('Contexto clínico sintético.');
  });

  it('confirma o registro no prontuário só depois da declaração do médico', async () => {
    const { fetchMock, usuario } = abrir({
      [`GET ${BASE}`]: json(200, analise()),
      [`POST ${BASE}/registro-prontuario`]: json(200, analise({ registradoProntuarioEm: '2026-11-04T14:00:00Z' })),
    });

    const confirmar = await screen.findByRole('button', { name: 'Confirmar registro no prontuário' });
    expect(confirmar).toBeDisabled();
    await usuario.click(screen.getByRole('checkbox', { name: /Registrei no prontuário/ }));
    await usuario.click(confirmar);

    expect(await screen.findByText(/^Registro no prontuário confirmado em/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Confirmar registro no prontuário' })).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([url, init]) => url === `${BASE}/registro-prontuario` && init?.method === 'POST')).toBe(true);
  });

  it('avisa quando o resultado não existe ou é de outro médico', async () => {
    abrir({ [`GET ${BASE}`]: json(404, { title: 'Análise não encontrada.' }) });
    expect(await screen.findByRole('heading', { name: 'Resultado não encontrado' })).toBeInTheDocument();
  });
});
