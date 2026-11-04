import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import { describe, expect, it } from 'vitest';
import { analise, ID_ANALISE } from './analises';
import { ID_SESSAO, sessaoColeta } from './coletas';
import { detalhe, ID_PACIENTE, pacienteResumo, sessao, tcle } from './pacientes';
import { ADMIN, comSessao, json, renderizarApp, simularApi } from './utilitarios';

/**
 * axe-core (WCAG 2.1 A/AA) em cada tela, com dados sintéticos. O contraste fica de
 * fora porque o jsdom não calcula cores; ele é conferido no navegador real (doc 09).
 */
async function semViolacoes() {
  const resultado = await axe.run(document.body, {
    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'best-practice'] },
    rules: { 'color-contrast': { enabled: false } },
  });
  const resumo = resultado.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`);
  expect(resumo).toEqual([]);
}

const PACIENTE = `/api/pacientes/${ID_PACIENTE}`;

describe('acessibilidade (axe)', () => {
  it('Entrar, com erros de validação', async () => {
    simularApi({});
    renderizarApp('/entrar');
    await userEvent.setup().click(screen.getByRole('button', { name: 'Entrar' }));
    await semViolacoes();
  });

  it('Solicitar cadastro, com erros de validação', async () => {
    simularApi({});
    renderizarApp('/cadastro');
    await userEvent.setup().click(screen.getByRole('button', { name: 'Enviar pedido' }));
    await semViolacoes();
  });

  it('Cadastros pendentes, com a decisão aberta', async () => {
    comSessao(ADMIN);
    simularApi({
      'GET /api/medicos/solicitacoes': json(200, [
        { id: 'a', nome: 'Dr. Sintético', email: 'x@exemplo.test', crm: '1', ufCrm: 'SP', especialidade: 'Pediatria', solicitadoEm: '2026-11-01T10:00:00Z' },
      ]),
    });
    renderizarApp('/admin/solicitacoes');
    await userEvent.setup().click(await screen.findByRole('button', { name: /^Analisar pedido/ }));
    await semViolacoes();
  });

  it('Pacientes', async () => {
    comSessao();
    simularApi({ 'GET /api/pacientes': json(200, [pacienteResumo({ podeColetar: true }), pacienteResumo({ id: 'b' })]) });
    renderizarApp('/pacientes');
    await screen.findByRole('table');
    await semViolacoes();
  });

  it('Cadastrar paciente, com erros', async () => {
    comSessao();
    simularApi({});
    renderizarApp('/pacientes/novo');
    await userEvent.setup().click(screen.getByRole('button', { name: 'Cadastrar paciente' }));
    await semViolacoes();
  });

  it('Paciente, com formulário de consentimento e confirmação de revogação', async () => {
    comSessao();
    simularApi({
      [`GET ${PACIENTE}`]: json(200, detalhe({ consentimentos: [tcle()], sessoes: [sessao({ status: 'COMPLETA', analiseId: 'x', analiseStatus: 'CONCLUIDA' })] }, { podeColetar: true })),
    });
    renderizarApp(`/pacientes/${ID_PACIENTE}`);
    const usuario = userEvent.setup();
    await usuario.click(await screen.findByRole('button', { name: 'Registrar consentimento' }));
    await usuario.click(screen.getByRole('radio', { name: /^TCLE/ }));
    await usuario.click(screen.getByRole('button', { name: /^Revogar TCLE/ }));
    await semViolacoes();
  });

  it('Coleta, com pré-visualização e dedo não coletável', async () => {
    comSessao();
    simularApi({
      [`GET ${PACIENTE}`]: json(200, detalhe()),
      [`GET /api/sessoes/${ID_SESSAO}`]: json(200, sessaoColeta({}, ['COLETADO', 'NAO_COLETAVEL'])),
    });
    renderizarApp(`/sessoes/${ID_SESSAO}`);
    const usuario = userEvent.setup();
    await usuario.upload(await screen.findByLabelText(/^Imagem do/), new File([new Uint8Array(8)], 'd.png', { type: 'image/png' }));
    await usuario.click(screen.getByRole('button', { name: 'Este dedo não pode ser coletado' }));
    await semViolacoes();
  });

  it('Resultado da análise', async () => {
    comSessao();
    simularApi({ [`GET ${PACIENTE}`]: json(200, detalhe()), [`GET /api/analises/${ID_ANALISE}`]: json(200, analise()) });
    renderizarApp(`/analises/${ID_ANALISE}`);
    await screen.findByRole('region', { name: 'Indicadores' });
    await semViolacoes();
  });
});
