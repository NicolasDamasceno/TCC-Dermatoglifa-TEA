import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { detalhe, ID_PACIENTE, pacienteResumo } from '../testes/pacientes';
import { comSessao, json, renderizarApp, simularFetch } from '../testes/utilitarios';

function abrir(rota: string, ...respostas: Response[]) {
  comSessao();
  const fetchMock = simularFetch(...respostas);
  renderizarApp(rota);
  return fetchMock;
}

describe('Pacientes (UC04)', () => {
  it('lista os pacientes com idade, sexo, situação e se a coleta está liberada', async () => {
    abrir(
      '/pacientes',
      json(200, [
        pacienteResumo({ podeColetar: true }),
        pacienteResumo({ id: 'b', nome: 'Outra Criança Sintética', sexo: 'MASCULINO', dataNascimento: '2016-01-01' }),
        pacienteResumo({ id: 'c', nome: null, dataNascimento: null, idadeAnos: null, anonimizado: true }),
      ]),
    );

    const tabela = await screen.findByRole('table', { name: /3 pacientes/ });
    const primeira = within(tabela).getByRole('row', { name: /^Criança Sintética/ });
    expect(within(primeira).getByRole('link', { name: 'Criança Sintética' })).toHaveAttribute(
      'href',
      `/pacientes/${ID_PACIENTE}`,
    );
    expect(primeira).toHaveTextContent('Feminino');
    expect(primeira).toHaveTextContent('Suspeita de TEA');
    expect(primeira).toHaveTextContent('Liberada');
    expect(within(tabela).getByRole('row', { name: /Outra Criança/ })).toHaveTextContent('Falta TCLE');
    expect(within(tabela).getByRole('row', { name: /Dados eliminados/ })).toHaveTextContent('Indisponível');
  });

  it('convida a cadastrar o primeiro paciente quando a lista está vazia', async () => {
    abrir('/pacientes', json(200, []));
    expect(await screen.findByText('Nenhum paciente cadastrado ainda.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Cadastrar o primeiro paciente' })).toHaveAttribute('href', '/pacientes/novo');
  });

  it('busca por nome e permite limpar a busca', async () => {
    const fetchMock = abrir('/pacientes', json(200, [pacienteResumo()]), json(200, []), json(200, [pacienteResumo()]));
    const usuario = userEvent.setup();
    await screen.findByRole('table');

    await usuario.type(screen.getByRole('searchbox', { name: 'Buscar por nome' }), ' zé ');
    await usuario.click(screen.getByRole('button', { name: 'Buscar' }));

    expect(await screen.findByText('Nenhum paciente com “zé” no nome.')).toBeInTheDocument();
    expect(fetchMock.mock.calls[1]![0]).toBe('/api/pacientes?busca=z%C3%A9');

    await usuario.click(screen.getByRole('button', { name: 'Limpar busca' }));
    expect(await screen.findByRole('table')).toBeInTheDocument();
    expect(fetchMock.mock.calls[2]![0]).toBe('/api/pacientes');
    expect(screen.getByRole('searchbox')).toHaveValue('');
  });

  it('oferece tentar de novo quando a lista falha', async () => {
    abrir('/pacientes', json(503, {}), json(200, [pacienteResumo()]));
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('table')).toBeInTheDocument();
  });

  it('abre o paciente pelo nome', async () => {
    abrir('/pacientes', json(200, [pacienteResumo()]), json(200, detalhe()));
    await userEvent.setup().click(await screen.findByRole('link', { name: 'Criança Sintética' }));
    expect(await screen.findByRole('heading', { name: 'Criança Sintética', level: 1 })).toBeInTheDocument();
  });
});
