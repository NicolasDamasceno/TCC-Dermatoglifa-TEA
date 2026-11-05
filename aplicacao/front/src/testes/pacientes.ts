import type { Consentimento, PacienteDetalhe, PacienteResumo, SessaoDoPaciente } from '../api/pacientes';

/** Pacientes sintéticos para os testes — nenhum dado real (CLAUDE.md, regra 4). */
export const ID_PACIENTE = '00000000-0000-0000-0000-00000000c0de';

export function pacienteResumo(extra: Partial<PacienteResumo> = {}): PacienteResumo {
  return {
    id: ID_PACIENTE,
    nome: 'Criança Sintética',
    dataNascimento: '2022-03-14',
    idadeAnos: 4,
    sexo: 'FEMININO',
    situacao: 'SUSPEITA',
    podeColetar: false,
    anonimizado: false,
    criadoEm: '2026-11-01T12:00:00Z',
    ...extra,
  };
}

export function tcle(extra: Partial<Consentimento> = {}): Consentimento {
  return {
    id: '00000000-0000-0000-0000-0000000000c1',
    tipo: 'TCLE',
    versaoTermo: 'CEP-v1.0',
    assinadoEm: '2026-11-03',
    autorizaAnalise: true,
    cienteUsoIA: true,
    autorizaUsoTreino: false,
    vigente: true,
    revogadoEm: null,
    ...extra,
  };
}

export function sessao(extra: Partial<SessaoDoPaciente> = {}): SessaoDoPaciente {
  return {
    id: '00000000-0000-0000-0000-0000000005e5',
    status: 'EM_ANDAMENTO',
    iniciadaEm: '2026-11-04T13:00:00Z',
    concluidaEm: null,
    analiseId: null,
    analiseStatus: null,
    analiseConcluidaEm: null,
    versaoModelo: null,
    origemDadosModelo: null,
    ...extra,
  };
}

export function detalhe(extra: Partial<PacienteDetalhe> = {}, paciente: Partial<PacienteResumo> = {}): PacienteDetalhe {
  return {
    paciente: pacienteResumo(paciente),
    responsavel: { nome: 'Responsável Sintético', parentesco: 'MAE', contato: '(00) 90000-0000' },
    diagnostico: null,
    consentimentos: [],
    sessoes: [],
    ...extra,
  };
}
