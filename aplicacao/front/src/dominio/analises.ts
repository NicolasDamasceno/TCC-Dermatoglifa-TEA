import type { OrigemDados, PadraoDigital } from '../api/analises';

export const NOME_PADRAO: Readonly<Record<PadraoDigital, string>> = {
  ARCO: 'Arco',
  ARCO_TENDIDO: 'Arco tendido',
  ALCA_ULNAR: 'Alça ulnar',
  ALCA_RADIAL: 'Alça radial',
  VERTICILO: 'Verticilo',
};

export const NOME_ORIGEM: Readonly<Record<OrigemDados, string>> = {
  SINTETICO: 'Dados sintéticos',
  REAL: 'Dados de participantes',
};

/** Nomes das métricas mais comuns; as demais aparecem com a chave original. */
const NOME_METRICA: Readonly<Record<string, string>> = {
  acuracia: 'Acurácia',
  acuraciaBalanceada: 'Acurácia balanceada',
  sensibilidade: 'Sensibilidade',
  especificidade: 'Especificidade',
  auc: 'AUC',
  f1: 'F1',
  f1Macro: 'F1 macro',
  kappa: 'Kappa',
  n: 'Imagens no teste',
  nTeste: 'Imagens no teste',
};

/** Métricas conhecidas primeiro, na ordem acima; o banco (jsonb) não guarda a ordem. */
export function ordemMetrica(chave: string): number {
  const posicao = Object.keys(NOME_METRICA).indexOf(chave);
  return posicao < 0 ? Number.MAX_SAFE_INTEGER : posicao;
}

export function nomeMetrica(chave: string): string {
  return NOME_METRICA[chave] ?? chave;
}

/** 0,91 → "91%". */
export function percentual(valor: number): string {
  return `${String(Math.round(valor * 100))}%`;
}

/** Números das métricas em português, com até 3 casas: 0.8834 → "0,883". */
export function numero(valor: number): string {
  return valor.toLocaleString('pt-BR', { maximumFractionDigits: 3 });
}
