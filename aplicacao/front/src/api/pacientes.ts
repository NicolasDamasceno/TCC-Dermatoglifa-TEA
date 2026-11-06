import type { ArquivoBaixado, ClienteApi } from './cliente';

export type Sexo = 'MASCULINO' | 'FEMININO';
export type Situacao = 'SUSPEITA' | 'TEA_CONFIRMADO' | 'TEA_DESCARTADO' | 'CONTROLE';
export type TipoTermo = 'TCLE' | 'TALE';
export type StatusSessao = 'EM_ANDAMENTO' | 'COMPLETA' | 'INCOMPLETA' | 'CANCELADA';
export type StatusAnalise = 'PENDENTE' | 'PROCESSANDO' | 'CONCLUIDA' | 'ERRO';
export type MetodoCaptura = 'LEITOR_OPTICO' | 'TINTA_DIGITALIZADA';
export type TipoImpressao = 'ROLADA' | 'PLANA';

/** Item de GET /api/pacientes e bloco `paciente` do detalhe (doc 08, seção 5). */
export interface PacienteResumo {
  id: string;
  /** null quando o titular pediu a eliminação dos dados (UC12). */
  nome: string | null;
  dataNascimento: string | null;
  idadeAnos: number | null;
  sexo: Sexo;
  situacao: Situacao;
  podeColetar: boolean;
  anonimizado: boolean;
  criadoEm: string;
}

export interface Responsavel {
  nome: string;
  parentesco: string | null;
  contato: string | null;
}

export interface Consentimento {
  id: string;
  tipo: TipoTermo;
  versaoTermo: string;
  assinadoEm: string;
  autorizaAnalise: boolean;
  cienteUsoIA: boolean;
  autorizaUsoTreino: boolean;
  vigente: boolean;
  revogadoEm: string | null;
}

export interface SessaoDoPaciente {
  id: string;
  status: StatusSessao;
  iniciadaEm: string;
  concluidaEm: string | null;
  analiseId: string | null;
  analiseStatus: StatusAnalise | null;
  /** UC09: quando a análise foi concluída e com qual versão do modelo (RN06). */
  analiseConcluidaEm: string | null;
  versaoModelo: string | null;
  origemDadosModelo: 'SINTETICO' | 'REAL' | null;
}

/** Como a situação foi definida (UC13); null enquanto o paciente é suspeita. */
export interface Diagnostico {
  fonte: string;
  avaliadoEm: string;
  atualizadoEm: string;
}

export interface PacienteDetalhe {
  paciente: PacienteResumo;
  responsavel: Responsavel | null;
  diagnostico: Diagnostico | null;
  consentimentos: Consentimento[];
  sessoes: SessaoDoPaciente[];
}

export interface NovoPaciente {
  nome: string;
  dataNascimento: string;
  sexo: Sexo;
  responsavel: Responsavel | null;
}

export interface NovoConsentimento {
  tipo: TipoTermo;
  versaoTermo: string;
  assinadoEm: string;
  autorizaAnalise: boolean;
  cienteUsoIA: boolean;
  autorizaUsoTreino: boolean;
}

export interface ResultadoConsentimento {
  consentimentoId: string;
  podeColetar: boolean;
}

/** PATCH /api/pacientes/{id}/situacao-diagnostica (UC13; ADR-0009). */
export interface NovaSituacao {
  situacao: Situacao;
  fonte: string | null;
  avaliadoEm: string | null;
  independenteDoSistema: boolean;
}

export interface SituacaoAtualizada {
  situacao: Situacao;
  fonteDiagnostico: string | null;
  avaliadoEm: string | null;
  atualizadaEm: string;
}

export interface NovaSessao {
  metodo: MetodoCaptura;
  tipoImpressao: TipoImpressao;
  dispositivo: string | null;
}

export const chavesPacientes = {
  todas: ['pacientes'] as const,
  lista: (busca: string) => ['pacientes', 'lista', busca] as const,
  detalhe: (id: string) => ['pacientes', 'detalhe', id] as const,
};

const caminho = (id: string) => `/api/pacientes/${encodeURIComponent(id)}`;

export function listarPacientes(api: ClienteApi, busca: string): Promise<PacienteResumo[]> {
  const consulta = busca ? `?busca=${encodeURIComponent(busca)}` : '';
  return api.requisitar('GET', `/api/pacientes${consulta}`);
}

export function obterPaciente(api: ClienteApi, id: string): Promise<PacienteDetalhe> {
  return api.requisitar('GET', caminho(id));
}

export function cadastrarPaciente(api: ClienteApi, dados: NovoPaciente): Promise<{ id: string; podeColetar: boolean }> {
  return api.requisitar('POST', '/api/pacientes', dados);
}

export function registrarConsentimento(
  api: ClienteApi,
  pacienteId: string,
  dados: NovoConsentimento,
): Promise<ResultadoConsentimento> {
  return api.requisitar('POST', `${caminho(pacienteId)}/consentimentos`, dados);
}

export function revogarConsentimento(
  api: ClienteApi,
  pacienteId: string,
  consentimentoId: string,
): Promise<ResultadoConsentimento> {
  return api.requisitar('POST', `${caminho(pacienteId)}/consentimentos/${encodeURIComponent(consentimentoId)}/revogacao`);
}

/** UC13 — registra a situação definida pelo diagnóstico clínico, feito fora do sistema. */
export function atualizarSituacao(api: ClienteApi, pacienteId: string, dados: NovaSituacao): Promise<SituacaoAtualizada> {
  return api.requisitar('PATCH', `${caminho(pacienteId)}/situacao-diagnostica`, dados);
}

/** UC12 (administrador): dados corrigíveis da identificação; o sexo fica no paciente pseudônimo. */
export interface NovaIdentificacao {
  nome: string;
  dataNascimento: string;
  responsavel: Responsavel | null;
}

/** UC12 — pacote .zip com os dados e as imagens do paciente (LGPD art. 18, II e V). */
export function exportarDados(api: ClienteApi, pacienteId: string): Promise<ArquivoBaixado> {
  return api.baixar(`${caminho(pacienteId)}/exportacao`);
}

/** UC12 — corrige a identificação (LGPD art. 18, III). */
export function corrigirIdentificacao(api: ClienteApi, pacienteId: string, dados: NovaIdentificacao): Promise<void> {
  return api.requisitar('PUT', `${caminho(pacienteId)}/identificacao`, dados);
}

/** UC12 — elimina identificação, imagens e textos livres (RN08). Irreversível. */
export function eliminarDados(api: ClienteApi, pacienteId: string, confirmacao: string): Promise<{ imagensApagadas: number }> {
  return api.requisitar('POST', `${caminho(pacienteId)}/eliminacao`, { confirmacao });
}

/** Inicia a sessão de coleta (UC06); a resposta traz o id para abrir a ficha. */
export function iniciarSessao(api: ClienteApi, pacienteId: string, dados: NovaSessao): Promise<{ id: string }> {
  return api.requisitar('POST', `${caminho(pacienteId)}/sessoes`, dados);
}
