import type { ClienteApi } from './cliente';

export type OrigemDados = 'SINTETICO' | 'REAL';

/** Modelo que o Serviço de IA está usando agora, comparado com o registro (UC10). */
export interface ServicoIA {
  disponivel: boolean;
  versaoModelo: string | null;
  origemDados: OrigemDados | null;
  sha256: string | null;
  /** DEMO ou PRODUCAO: de qual pasta o serviço carregou o modelo. */
  pastaOrigem: string | null;
  motivo: string | null;
  registrado: boolean;
  ativo: boolean;
  hashConfere: boolean;
}

export interface Metricas {
  acuracia?: number;
  acuraciaBalanceada?: number;
  nTeste?: number;
  auc?: number;
}

export interface ModeloIA {
  id: string;
  versao: string;
  arquitetura: string;
  origemDados: OrigemDados;
  escoreValidado: boolean;
  hashSha256: string;
  metricas: Metricas;
  treinadoEm: string;
  ativo: boolean;
  promovidoEm: string | null;
  analises: number;
}

export interface PainelModelos {
  servico: ServicoIA;
  modelos: ModeloIA[];
}

export const CHAVE_MODELOS = ['modelos'] as const;

export function obterModelos(api: ClienteApi): Promise<PainelModelos> {
  return api.requisitar('GET', '/api/modelos');
}

/** UC10 — registra (ou reativa) e ativa a versão que o Serviço de IA carregou. */
export function promoverModelo(api: ClienteApi, config: File, metricas: File): Promise<ModeloIA> {
  const dados = new FormData();
  dados.append('config', config);
  dados.append('metricas', metricas);
  return api.requisitar('POST', '/api/modelos/promocao', dados);
}
