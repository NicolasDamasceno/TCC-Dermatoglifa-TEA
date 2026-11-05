import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { detalhe, ID_PACIENTE, sessao, tcle } from '../../testes/pacientes';
import { ID_SESSAO, sessaoColeta } from '../../testes/coletas';
import { comSessao, json, renderizarApp, simularApi, simularFetch } from '../../testes/utilitarios';

const ROTA = `/pacientes/${ID_PACIENTE}`;

function abrir(...respostas: Response[]) {
  comSessao();
  const fetchMock = simularFetch(...respostas);
  renderizarApp(ROTA);
  return { fetchMock, usuario: userEvent.setup() };
}

function corpo(fetchMock: ReturnType<typeof simularFetch>, chamada: number): unknown {
  return JSON.parse(fetchMock.mock.calls[chamada]![1]!.body as string);
}

describe('Paciente (UC04, UC05, UC09)', () => {
  it('mostra identificação, responsável e por que a coleta não está liberada', async () => {
    abrir(json(200, detalhe()));

    expect(await screen.findByRole('heading', { name: 'Criança Sintética', level: 1 })).toBeInTheDocument();
    expect(document.title).toBe('Criança Sintética | Dermatoglifia TEA');
    expect(screen.getByText('14/03/2022')).toBeInTheDocument();
    expect(screen.getByText('Suspeita de TEA')).toBeInTheDocument();
    expect(screen.getByText('Mãe')).toBeInTheDocument();
    expect(screen.getByText(/A coleta ainda não está liberada/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Iniciar coleta' })).not.toBeInTheDocument();
    expect(screen.getByText('Nenhum termo registrado.')).toBeInTheDocument();
  });

  it('registra o TCLE com as autorizações e libera a coleta', async () => {
    const { fetchMock, usuario } = abrir(
      json(200, detalhe()),
      json(201, { consentimentoId: tcle().id, podeColetar: true }),
      json(200, detalhe({ consentimentos: [tcle()] }, { podeColetar: true })),
    );

    const secaoColeta = await screen.findByRole('region', { name: 'Coleta das digitais' });
    await usuario.click(within(secaoColeta).getByRole('button', { name: 'Registrar o TCLE' }));
    const formulario = screen.getByRole('form', { name: 'Registrar consentimento' });
    expect(within(formulario).getByRole('radio', { name: /^TCLE/ })).toHaveFocus();

    await usuario.click(within(formulario).getByRole('radio', { name: /^TCLE/ }));
    await usuario.type(within(formulario).getByLabelText('Versão do termo'), 'CEP-v1.0');
    await usuario.type(within(formulario).getByLabelText('Data de assinatura'), '2026-11-03');
    await usuario.click(within(formulario).getByRole('checkbox', { name: /Autoriza a análise/ }));
    expect(within(formulario).getByText(/a coleta continua bloqueada/)).toBeInTheDocument();
    await usuario.click(within(formulario).getByRole('checkbox', { name: /ciente do uso de IA/ }));
    expect(within(formulario).queryByText(/a coleta continua bloqueada/)).not.toBeInTheDocument();
    await usuario.click(within(formulario).getByRole('button', { name: 'Registrar TCLE' }));

    expect(await screen.findByText('TCLE registrado. Coleta liberada.')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Iniciar coleta' })).toBeInTheDocument();
    expect(fetchMock.mock.calls[1]![0]).toBe(`/api/pacientes/${ID_PACIENTE}/consentimentos`);
    expect(corpo(fetchMock, 1)).toEqual({
      tipo: 'TCLE',
      versaoTermo: 'CEP-v1.0',
      assinadoEm: '2026-11-03',
      autorizaAnalise: true,
      cienteUsoIA: true,
      autorizaUsoTreino: false,
    });
    const linha = screen.getByRole('row', { name: /TCLE versão CEP-v1.0/ });
    expect(linha).toHaveTextContent('Uso em treino de modelos: não');
    expect(linha).toHaveTextContent('Vigente');
  });

  it('registra o TALE sem autorizações e explica que ele não libera sozinho', async () => {
    const { fetchMock, usuario } = abrir(
      json(200, detalhe()),
      json(201, { consentimentoId: 'tale', podeColetar: false }),
      json(200, detalhe({ consentimentos: [tcle({ id: 'tale', tipo: 'TALE', autorizaAnalise: false, cienteUsoIA: false })] })),
    );

    await usuario.click(await screen.findByRole('button', { name: 'Registrar consentimento' }));
    await usuario.click(screen.getByRole('radio', { name: /^TALE/ }));
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    await usuario.type(screen.getByLabelText('Versão do termo'), 'CEP-v1.0');
    await usuario.type(screen.getByLabelText('Data de assinatura'), '2026-11-03');
    await usuario.click(screen.getByRole('button', { name: 'Registrar TALE' }));

    expect(await screen.findByText('TALE registrado. A coleta é liberada pelo TCLE do responsável.')).toBeInTheDocument();
    expect(corpo(fetchMock, 1)).toMatchObject({ tipo: 'TALE', autorizaAnalise: false, cienteUsoIA: false });
  });

  it('confere tipo, versão e data antes de registrar', async () => {
    const { fetchMock, usuario } = abrir(json(200, detalhe()));

    await usuario.click(await screen.findByRole('button', { name: 'Registrar consentimento' }));
    await usuario.click(screen.getByRole('button', { name: 'Registrar termo' }));

    expect(screen.getByRole('group', { name: 'Tipo de termo' })).toHaveAccessibleDescription('Escolha o tipo de termo.');
    expect(screen.getByLabelText('Versão do termo')).toHaveAccessibleDescription(
      'Como aprovada pelo CEP, por exemplo CEP-v1.0. Informe a versão do termo.',
    );
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('revoga um termo depois de confirmar', async () => {
    const { fetchMock, usuario } = abrir(
      json(200, detalhe({ consentimentos: [tcle()] }, { podeColetar: true })),
      json(200, { consentimentoId: tcle().id, podeColetar: false }),
      json(200, detalhe({ consentimentos: [tcle({ vigente: false, revogadoEm: '2026-11-04T15:00:00Z' })] })),
    );

    await usuario.click(await screen.findByRole('button', { name: 'Revogar TCLE de 03/11/2026' }));
    const confirmacao = screen.getByRole('group', { name: 'Confirmar revogação do TCLE de 03/11/2026' });
    await usuario.click(within(confirmacao).getByRole('button', { name: 'Confirmar revogação' }));

    expect(await screen.findByText('TCLE de 03/11/2026 revogado. A coleta não está mais liberada.')).toBeInTheDocument();
    expect(await screen.findByText('Revogado em 04/11/2026')).toBeInTheDocument();
    expect(fetchMock.mock.calls[1]![0]).toBe(`/api/pacientes/${ID_PACIENTE}/consentimentos/${tcle().id}/revogacao`);
  });

  it('desiste da revogação sem chamar a API', async () => {
    const { fetchMock, usuario } = abrir(json(200, detalhe({ consentimentos: [tcle()] }, { podeColetar: true })));

    await usuario.click(await screen.findByRole('button', { name: 'Revogar TCLE de 03/11/2026' }));
    await usuario.click(screen.getByRole('button', { name: 'Manter o termo' }));

    expect(screen.getByRole('button', { name: 'Revogar TCLE de 03/11/2026' })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('inicia a coleta com método e tipo de impressão e abre a ficha', async () => {
    comSessao();
    const fetchMock = simularApi({
      [`GET ${'/api/pacientes/' + ID_PACIENTE}`]: [
        json(200, detalhe({ consentimentos: [tcle()] }, { podeColetar: true })),
        json(200, detalhe({ consentimentos: [tcle()], sessoes: [sessao()] }, { podeColetar: true })),
      ],
      [`POST /api/pacientes/${ID_PACIENTE}/sessoes`]: json(201, sessaoColeta()),
      [`GET /api/sessoes/${ID_SESSAO}`]: json(200, sessaoColeta()),
    });
    renderizarApp(ROTA);
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Iniciar coleta' }));
    await usuario.click(screen.getByRole('button', { name: 'Iniciar coleta' }));
    expect(screen.getByRole('group', { name: 'Método de captura' })).toHaveAccessibleDescription(
      'Escolha o método de captura.',
    );
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await usuario.click(screen.getByRole('radio', { name: 'Leitor óptico' }));
    await usuario.click(screen.getByRole('radio', { name: /^Rolada/ }));
    await usuario.type(screen.getByLabelText('Dispositivo (opcional)'), 'Leitor sintético');
    await usuario.click(screen.getByRole('button', { name: 'Iniciar coleta' }));

    expect(await screen.findByRole('heading', { name: 'Coleta das digitais', level: 1 })).toBeInTheDocument();
    const inicio = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')!;
    expect(JSON.parse(inicio[1]!.body as string)).toEqual({
      metodo: 'LEITOR_OPTICO',
      tipoImpressao: 'ROLADA',
      dispositivo: 'Leitor sintético',
    });
  });

  it('oferece continuar a coleta em andamento e abrir resultados anteriores', async () => {
    abrir(
      json(
        200,
        detalhe(
          {
            consentimentos: [tcle()],
            sessoes: [
              sessao(),
              sessao({
                id: 'antiga',
                status: 'COMPLETA',
                iniciadaEm: '2026-10-01T13:00:00Z',
                concluidaEm: '2026-10-01T13:20:00Z',
                analiseId: 'analise-1',
                analiseStatus: 'CONCLUIDA',
                analiseConcluidaEm: '2026-10-01T13:21:00Z',
                versaoModelo: '2026-11-04_padroes_sintetico_v1',
                origemDadosModelo: 'SINTETICO',
              }),
            ],
          },
          { podeColetar: true },
        ),
      ),
    );

    const coleta = await screen.findByRole('region', { name: 'Coleta das digitais' });
    expect(within(coleta).getByRole('link', { name: 'Continuar coleta' })).toHaveAttribute('href', `/sessoes/${sessao().id}`);
    expect(within(coleta).queryByRole('button', { name: 'Iniciar coleta' })).not.toBeInTheDocument();

    const coletas = screen.getByRole('region', { name: 'Coletas' });
    expect(within(coletas).getByRole('link', { name: /^Ver o resultado da coleta de/ })).toHaveAttribute(
      'href',
      '/analises/analise-1',
    );
    const completa = within(coletas).getByRole('row', { name: /Completa/ });
    expect(completa).toHaveTextContent('Concluída');
    // UC09: a versão do modelo de cada análise, com a origem dos dados.
    expect(completa).toHaveTextContent('2026-11-04_padroes_sintetico_v1');
    expect(completa).toHaveTextContent('treinado com dados sintéticos');
    expect(within(coletas).getByRole('row', { name: /Em andamento/ })).toHaveTextContent('—');
  });

  it('explica que a coleta em andamento não continua sem TCLE vigente', async () => {
    abrir(json(200, detalhe({ consentimentos: [tcle({ vigente: false, revogadoEm: '2026-11-04T15:00:00Z' })], sessoes: [sessao()] })));

    const coleta = await screen.findByRole('region', { name: 'Coleta das digitais' });
    expect(coleta).toHaveTextContent('ela não pode continuar');
    expect(within(coleta).queryByRole('link', { name: 'Continuar coleta' })).not.toBeInTheDocument();
    expect(within(coleta).getByRole('link', { name: 'Ver a coleta' })).toHaveAttribute('href', `/sessoes/${sessao().id}`);
    expect(within(coleta).getByRole('button', { name: 'Registrar o TCLE' })).toBeInTheDocument();
  });

  it('avisa quando o paciente não existe ou é de outro médico', async () => {
    abrir(json(404, { title: 'Paciente não encontrado.' }));
    expect(await screen.findByRole('heading', { name: 'Paciente não encontrado' })).toBeInTheDocument();
  });

  it('não oferece coleta nem consentimento para dados eliminados', async () => {
    abrir(json(200, detalhe({ responsavel: null }, { nome: null, dataNascimento: null, anonimizado: true })));

    expect(await screen.findByRole('heading', { name: 'Dados eliminados a pedido do titular' })).toBeInTheDocument();
    expect(screen.getByText(/foram eliminados a pedido do titular/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Registrar consentimento' })).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Consentimentos' })).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Situação diagnóstica' })).not.toBeInTheDocument();
  });
});

const CONFIRMADO = { fonte: 'Avaliação multiprofissional sintética', avaliadoEm: '2026-10-20', atualizadoEm: '2026-10-21T14:00:00Z' };
const INDEPENDENTE = /sem considerar o resultado/;

describe('Situação diagnóstica (UC13)', () => {
  it('registra o diagnóstico com fonte, data e a declaração de independência', async () => {
    const { fetchMock, usuario } = abrir(
      json(200, detalhe()),
      json(200, { situacao: 'TEA_CONFIRMADO', fonteDiagnostico: CONFIRMADO.fonte, avaliadoEm: CONFIRMADO.avaliadoEm, atualizadaEm: CONFIRMADO.atualizadoEm }),
      json(200, detalhe({ diagnostico: CONFIRMADO }, { situacao: 'TEA_CONFIRMADO' })),
    );

    const secao = await screen.findByRole('region', { name: 'Situação diagnóstica' });
    expect(secao).toHaveTextContent('Quando o diagnóstico clínico for fechado');
    await usuario.click(within(secao).getByRole('button', { name: 'Atualizar situação diagnóstica' }));
    const formulario = within(secao).getByRole('form', { name: 'Atualizar situação diagnóstica' });
    expect(within(formulario).getByRole('radio', { name: /^TEA confirmado/ })).toHaveFocus();
    expect(within(formulario).queryByRole('radio', { name: /Voltar para suspeita/ })).not.toBeInTheDocument();

    await usuario.click(within(formulario).getByRole('radio', { name: /^TEA confirmado/ }));
    await usuario.click(within(formulario).getByRole('button', { name: 'Salvar situação' }));
    expect(within(formulario).getByText('Informe como o diagnóstico foi obtido.')).toBeInTheDocument();
    expect(within(formulario).getByText('Confirme que o diagnóstico não considerou o resultado do sistema.')).toBeInTheDocument();
    expect(within(formulario).getByRole('checkbox', { name: INDEPENDENTE })).toHaveAttribute('aria-invalid', 'true');
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await usuario.type(within(formulario).getByLabelText('Fonte do diagnóstico'), CONFIRMADO.fonte);
    await usuario.type(within(formulario).getByLabelText('Data da avaliação clínica'), CONFIRMADO.avaliadoEm);
    await usuario.click(within(formulario).getByRole('checkbox', { name: INDEPENDENTE }));
    await usuario.click(within(formulario).getByRole('button', { name: 'Salvar situação' }));

    expect(await within(secao).findByText('Situação diagnóstica atualizada: TEA confirmado.')).toBeInTheDocument();
    expect(fetchMock.mock.calls[1]![0]).toBe(`/api/pacientes/${ID_PACIENTE}/situacao-diagnostica`);
    expect(fetchMock.mock.calls[1]![1]!.method).toBe('PATCH');
    expect(corpo(fetchMock, 1)).toEqual({
      situacao: 'TEA_CONFIRMADO',
      fonte: CONFIRMADO.fonte,
      avaliadoEm: CONFIRMADO.avaliadoEm,
      independenteDoSistema: true,
    });
    expect(await within(secao).findByText(CONFIRMADO.fonte)).toBeInTheDocument();
    expect(secao).toHaveTextContent('20/10/2026');
  });

  it('não aceita avaliação antes do nascimento', async () => {
    const { fetchMock, usuario } = abrir(json(200, detalhe()));

    await usuario.click(await screen.findByRole('button', { name: 'Atualizar situação diagnóstica' }));
    const formulario = screen.getByRole('form', { name: 'Atualizar situação diagnóstica' });
    await usuario.click(within(formulario).getByRole('radio', { name: /^Controle/ }));
    await usuario.type(within(formulario).getByLabelText('Fonte do diagnóstico'), 'Grupo controle do protocolo');
    await usuario.type(within(formulario).getByLabelText('Data da avaliação clínica'), '2021-01-10');
    await usuario.click(within(formulario).getByRole('checkbox', { name: INDEPENDENTE }));
    await usuario.click(within(formulario).getByRole('button', { name: 'Salvar situação' }));

    expect(within(formulario).getByText('A data da avaliação é anterior ao nascimento.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('corrige um registro feito por engano voltando para suspeita', async () => {
    const { fetchMock, usuario } = abrir(
      json(200, detalhe({ diagnostico: CONFIRMADO }, { situacao: 'TEA_CONFIRMADO' })),
      json(200, { situacao: 'SUSPEITA', fonteDiagnostico: null, avaliadoEm: null, atualizadaEm: '2026-11-05T12:00:00Z' }),
      json(200, detalhe()),
    );

    const secao = await screen.findByRole('region', { name: 'Situação diagnóstica' });
    expect(within(secao).getByText('Avaliação clínica em')).toBeInTheDocument();
    await usuario.click(within(secao).getByRole('button', { name: 'Atualizar situação diagnóstica' }));
    const formulario = within(secao).getByRole('form', { name: 'Atualizar situação diagnóstica' });
    // Valores atuais preenchidos; a declaração não.
    expect(within(formulario).getByRole('radio', { name: /^TEA confirmado/ })).toBeChecked();
    expect(within(formulario).getByLabelText('Fonte do diagnóstico')).toHaveValue(CONFIRMADO.fonte);
    expect(within(formulario).getByRole('checkbox', { name: INDEPENDENTE })).not.toBeChecked();

    await usuario.click(within(formulario).getByRole('radio', { name: /Voltar para suspeita/ }));
    expect(within(formulario).queryByLabelText('Fonte do diagnóstico')).not.toBeInTheDocument();
    await usuario.click(within(formulario).getByRole('button', { name: 'Salvar situação' }));

    expect(await within(secao).findByText('Situação diagnóstica atualizada: Suspeita de TEA.')).toBeInTheDocument();
    expect(corpo(fetchMock, 1)).toEqual({ situacao: 'SUSPEITA', fonte: null, avaliadoEm: null, independenteDoSistema: false });
  });

  it('mostra o motivo quando a API recusa', async () => {
    const { usuario } = abrir(json(200, detalhe()), json(422, { title: 'A data da avaliação clínica não pode estar no futuro.', status: 422 }));

    await usuario.click(await screen.findByRole('button', { name: 'Atualizar situação diagnóstica' }));
    const formulario = screen.getByRole('form', { name: 'Atualizar situação diagnóstica' });
    await usuario.click(within(formulario).getByRole('radio', { name: /^TEA descartado/ }));
    await usuario.type(within(formulario).getByLabelText('Fonte do diagnóstico'), 'Avaliação sintética');
    await usuario.type(within(formulario).getByLabelText('Data da avaliação clínica'), '2026-10-01');
    await usuario.click(within(formulario).getByRole('checkbox', { name: INDEPENDENTE }));
    await usuario.click(within(formulario).getByRole('button', { name: 'Salvar situação' }));

    expect(await within(formulario).findByText('A data da avaliação clínica não pode estar no futuro.')).toBeInTheDocument();
  });
});
