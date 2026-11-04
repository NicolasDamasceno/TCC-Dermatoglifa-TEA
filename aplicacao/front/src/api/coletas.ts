import type { Dedo, Mao } from '../dominio/dedos';
import type { ClienteApi } from './cliente';
import type { MetodoCaptura, StatusAnalise, StatusSessao, TipoImpressao } from './pacientes';

export type SituacaoDedo = 'PENDENTE' | 'COLETADO' | 'NAO_COLETAVEL';

export interface DedoSessao {
  mao: Mao;
  dedo: Dedo;
  situacao: SituacaoDedo;
  resolucaoDpi: number | null;
  motivoNaoColetavel: string | null;
  capturadaEm: string | null;
}

/** Formato único das rotas de coleta (doc 08, seção 5). */
export interface SessaoColeta {
  id: string;
  pacienteId: string;
  status: StatusSessao;
  metodo: MetodoCaptura;
  tipoImpressao: TipoImpressao;
  dispositivo: string | null;
  idadeMeses: number;
  iniciadaEm: string;
  concluidaEm: string | null;
  dedos: DedoSessao[];
  analiseId: string | null;
  analiseStatus: StatusAnalise | null;
}

export const chavesColetas = {
  sessao: (id: string) => ['sessoes', id] as const,
};

const caminho = (id: string) => `/api/sessoes/${encodeURIComponent(id)}`;
const caminhoDedo = (id: string, mao: Mao, dedo: Dedo) => `${caminho(id)}/digitais/${mao}/${dedo}`;

export function obterSessao(api: ClienteApi, id: string): Promise<SessaoColeta> {
  return api.requisitar('GET', caminho(id));
}

/** Envia (ou substitui) a imagem de um dedo; a API nunca a devolve depois. */
export function enviarDigital(api: ClienteApi, id: string, mao: Mao, dedo: Dedo, arquivo: File): Promise<SessaoColeta> {
  const dados = new FormData();
  dados.append('imagem', arquivo, arquivo.name);
  return api.requisitar('PUT', caminhoDedo(id, mao, dedo), dados);
}

export function marcarNaoColetavel(api: ClienteApi, id: string, mao: Mao, dedo: Dedo, motivo: string): Promise<SessaoColeta> {
  return api.requisitar('POST', `${caminhoDedo(id, mao, dedo)}/nao-coletavel`, { motivo });
}

export function concluirSessao(api: ClienteApi, id: string): Promise<SessaoColeta> {
  return api.requisitar('POST', `${caminho(id)}/conclusao`);
}

export function cancelarSessao(api: ClienteApi, id: string): Promise<SessaoColeta> {
  return api.requisitar('POST', `${caminho(id)}/cancelamento`);
}
