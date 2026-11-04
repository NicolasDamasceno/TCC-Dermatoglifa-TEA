import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { detalhe, ID_PACIENTE } from '../testes/pacientes';
import { comSessao, json, renderizarApp, simularFetch } from '../testes/utilitarios';

function abrir(...respostas: Response[]) {
  comSessao();
  const fetchMock = simularFetch(...respostas);
  renderizarApp('/pacientes/novo');
  return { fetchMock, usuario: userEvent.setup() };
}

const proximoAno = String(new Date().getFullYear() + 1);

describe('Cadastrar paciente (UC04)', () => {
  it('cadastra um adulto sem responsável e leva ao paciente pedindo o TCLE', async () => {
    const { fetchMock, usuario } = abrir(
      json(201, { id: ID_PACIENTE, podeColetar: false }),
      json(200, detalhe({ responsavel: null }, { nome: 'Adulto Sintético', dataNascimento: '1990-05-10' })),
    );

    expect(screen.getByRole('group', { name: 'Responsável legal (opcional)' })).toBeInTheDocument();
    await usuario.type(screen.getByLabelText('Nome completo'), ' Adulto Sintético ');
    await usuario.type(screen.getByLabelText('Data de nascimento'), '1990-05-10');
    await usuario.click(screen.getByRole('radio', { name: 'Masculino' }));
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar paciente' }));

    expect(await screen.findByRole('heading', { name: 'Adulto Sintético', level: 1 })).toBeInTheDocument();
    expect(screen.getByText('Paciente cadastrado. Registre o TCLE assinado para liberar a coleta.')).toBeInTheDocument();
    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('/api/pacientes');
    expect(JSON.parse(init!.body as string)).toEqual({
      nome: 'Adulto Sintético',
      dataNascimento: '1990-05-10',
      sexo: 'MASCULINO',
      responsavel: null,
    });
  });

  it('exige o responsável quando o paciente é menor de 18 anos', async () => {
    const { fetchMock, usuario } = abrir(json(201, { id: ID_PACIENTE, podeColetar: false }), json(200, detalhe()));

    await usuario.type(screen.getByLabelText('Nome completo'), 'Criança Sintética');
    await usuario.type(screen.getByLabelText('Data de nascimento'), '2022-03-14');
    await usuario.click(screen.getByRole('radio', { name: 'Feminino' }));
    expect(screen.getByRole('group', { name: 'Responsável legal' })).toBeInTheDocument();
    expect(screen.getByLabelText('Nome do responsável')).toBeRequired();

    await usuario.click(screen.getByRole('button', { name: 'Cadastrar paciente' }));
    expect(screen.getByLabelText('Nome do responsável')).toHaveFocus();
    expect(screen.getByLabelText('Nome do responsável')).toHaveAccessibleDescription('Informe o nome do responsável legal.');
    expect(screen.getByLabelText('Parentesco')).toHaveAccessibleDescription('Escolha o parentesco.');
    expect(fetchMock).not.toHaveBeenCalled();

    await usuario.type(screen.getByLabelText('Nome do responsável'), 'Responsável Sintético');
    await usuario.selectOptions(screen.getByLabelText('Parentesco'), 'MAE');
    await usuario.type(screen.getByLabelText('Contato (telefone ou e-mail)'), '(00) 90000-0000');
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar paciente' }));

    await screen.findByRole('heading', { name: 'Criança Sintética', level: 1 });
    expect(JSON.parse(fetchMock.mock.calls[0]![1]!.body as string)).toMatchObject({
      sexo: 'FEMININO',
      responsavel: { nome: 'Responsável Sintético', parentesco: 'MAE', contato: '(00) 90000-0000' },
    });
  });

  it('confere os campos antes de enviar', async () => {
    const { fetchMock, usuario } = abrir();

    await usuario.type(screen.getByLabelText('Data de nascimento'), `${proximoAno}-01-01`);
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar paciente' }));

    expect(screen.getByLabelText('Nome completo')).toHaveFocus();
    expect(screen.getByLabelText('Data de nascimento')).toHaveAccessibleDescription(
      'A data de nascimento não pode estar no futuro.',
    );
    expect(screen.getByRole('group', { name: 'Sexo biológico' })).toHaveAccessibleDescription(
      'Entra na análise porque as contagens dermatoglíficas variam por sexo. Escolha o sexo biológico.',
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('mostra a regra recusada pela API', async () => {
    const { usuario } = abrir(
      json(422, { title: 'Para menores de 18 anos, nome, parentesco e contato do responsável legal são obrigatórios.' }),
    );

    await usuario.type(screen.getByLabelText('Nome completo'), 'Adulto Sintético');
    await usuario.type(screen.getByLabelText('Data de nascimento'), '1990-05-10');
    await usuario.click(screen.getByRole('radio', { name: 'Feminino' }));
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar paciente' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('responsável legal são obrigatórios');
  });
});
