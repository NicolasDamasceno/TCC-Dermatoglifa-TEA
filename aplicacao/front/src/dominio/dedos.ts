/** Valores como a API envia (enums em SNAKE_CASE_UPPER, doc 08). */
export type Mao = 'DIREITA' | 'ESQUERDA';
export type Dedo = 'POLEGAR' | 'INDICADOR' | 'MEDIO' | 'ANELAR' | 'MINIMO';

/** Ordem da ficha decadactilar: mão direita em cima, polegar à esquerda. */
export const MAOS: readonly Mao[] = ['DIREITA', 'ESQUERDA'];
export const DEDOS: readonly Dedo[] = ['POLEGAR', 'INDICADOR', 'MEDIO', 'ANELAR', 'MINIMO'];

export const NOME_MAO: Readonly<Record<Mao, string>> = { DIREITA: 'Direita', ESQUERDA: 'Esquerda' };

export const NOME_DEDO: Readonly<Record<Dedo, string>> = {
  POLEGAR: 'Polegar',
  INDICADOR: 'Indicador',
  MEDIO: 'Médio',
  ANELAR: 'Anelar',
  MINIMO: 'Mínimo',
};
