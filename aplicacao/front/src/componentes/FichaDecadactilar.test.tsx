import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { FichaDecadactilar } from './FichaDecadactilar';

describe('FichaDecadactilar', () => {
  it('é uma tabela com mão por linha e dedo por coluna, na ordem da ficha', () => {
    render(<FichaDecadactilar legenda="Ficha sintética" quadro={(mao, dedo) => `${mao}-${dedo}`} />);

    const tabela = screen.getByRole('table', { name: 'Ficha sintética' });
    const colunas = within(tabela).getAllByRole('columnheader').map((c) => c.textContent);
    const linhas = within(tabela).getAllByRole('rowheader').map((c) => c.textContent);
    expect(colunas).toEqual(['Polegar', 'Indicador', 'Médio', 'Anelar', 'Mínimo']);
    expect(linhas).toEqual(['Direita', 'Esquerda']);

    const quadros = within(tabela).getAllByRole('cell').map((c) => c.textContent).filter(Boolean);
    expect(quadros).toHaveLength(10);
    expect(quadros[0]).toBe('DIREITA-POLEGAR');
    expect(quadros[9]).toBe('ESQUERDA-MINIMO');
  });

  it('fica escondida de leitores de tela quando é só ilustração', () => {
    const { container } = render(<FichaDecadactilar decorativa />);
    expect(container.querySelector('table')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });
});
