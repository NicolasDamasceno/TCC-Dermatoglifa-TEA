import type { Dedo, Mao } from '../dominio/dedos';
import type { ClienteApi } from './cliente';
import type { StatusAnalise } from './pacientes';

/** Padrão já convertido para ulnar/radial pela mão (ADR-0002). */
export type PadraoDigital = 'ARCO' | 'ARCO_TENDIDO' | 'ALCA_ULNAR' | 'ALCA_RADIAL' | 'VERTICILO';
export type OrigemDados = 'SINTETICO' | 'REAL';

export interface ResultadoDedo {
  mao: Mao;
  dedo: Dedo;
  padrao: PadraoDigital;
  confianca: number;
  alertaQualidade: boolean;
}

export interface Indicadores {
  qtdArcos: number;
  qtdArcosTendidos: number;
  qtdAlcasUlnares: number;
  qtdAlcasRadiais: number;
  qtdVerticilos: number;
  discordanciaBilateral: number;
  discordanciaBilateralComPolegar: number;
}

export interface ModeloAnalise {
  versao: string;
  origemDados: OrigemDados;
  escoreValidado: boolean;
  treinadoEm: string;
  /** JSON livre das métricas no conjunto de teste (doc 08). */
  metricas: Record<string, unknown>;
}

/** GET /api/analises/{id} (doc 08, seção 5). Aviso e alertas vêm do back, versionados. */
export interface Analise {
  id: string;
  sessaoId: string;
  pacienteId: string;
  status: StatusAnalise;
  aviso: { versao: string; texto: string };
  alertas: { codigo: string; texto: string }[];
  modelo: ModeloAnalise | null;
  digitais: ResultadoDedo[];
  indicadores: Indicadores | null;
  escoreRisco: number | null;
  observacaoMedico: string | null;
  registradoProntuarioEm: string | null;
  mensagemErro: string | null;
  solicitadaEm: string;
  concluidaEm: string | null;
}

export const chavesAnalises = {
  analise: (id: string) => ['analises', id] as const,
};

const caminho = (id: string) => `/api/analises/${encodeURIComponent(id)}`;

export function obterAnalise(api: ClienteApi, id: string): Promise<Analise> {
  return api.requisitar('GET', caminho(id));
}

export function novaTentativa(api: ClienteApi, id: string): Promise<Analise> {
  return api.requisitar('POST', `${caminho(id)}/nova-tentativa`);
}

export function salvarObservacao(api: ClienteApi, id: string, observacao: string | null): Promise<Analise> {
  return api.requisitar('PATCH', `${caminho(id)}/observacao`, { observacao });
}

export function confirmarRegistroProntuario(api: ClienteApi, id: string): Promise<Analise> {
  return api.requisitar('POST', `${caminho(id)}/registro-prontuario`);
}
