import type { Analise, ResultadoDedo } from '../api/analises';
import { DEDOS, MAOS } from '../dominio/dedos';
import { ID_SESSAO } from './coletas';
import { ID_PACIENTE } from './pacientes';

export const ID_ANALISE = '00000000-0000-0000-0000-0000000a0a11';

/** Análise sintética no formato do doc 08; textos curtos no lugar dos do back. */
export function analise(extra: Partial<Analise> = {}): Analise {
  const digitais: ResultadoDedo[] = MAOS.flatMap((mao) =>
    DEDOS.map((dedo) => ({
      mao,
      dedo,
      padrao: dedo === 'POLEGAR' ? 'VERTICILO' : mao === 'DIREITA' ? 'ALCA_ULNAR' : 'ARCO',
      confianca: 0.91,
      alertaQualidade: false,
    })),
  );

  return {
    id: ID_ANALISE,
    sessaoId: ID_SESSAO,
    pacienteId: ID_PACIENTE,
    status: 'CONCLUIDA',
    aviso: { versao: 'v1', texto: 'Protótipo de pesquisa (aviso sintético de teste).' },
    alertas: [
      { codigo: 'SEM_PONTO_DE_CORTE_VALIDADO', texto: 'Sem ponto de corte validado (teste).' },
      { codigo: 'ESCORE_NAO_VALIDADO', texto: 'Escore não validado (teste).' },
      { codigo: 'MODELO_SINTETICO', texto: 'Resultado de demonstração (teste).' },
    ],
    modelo: {
      versao: 'teste_sintetico_v1',
      origemDados: 'SINTETICO',
      escoreValidado: false,
      treinadoEm: '2026-11-01',
      metricas: { acuraciaBalanceada: 0.88, nTeste: 500 },
    },
    digitais,
    indicadores: {
      qtdArcos: 4,
      qtdArcosTendidos: 0,
      qtdAlcasUlnares: 4,
      qtdAlcasRadiais: 0,
      qtdVerticilos: 2,
      discordanciaBilateral: 4,
      discordanciaBilateralComPolegar: 4,
    },
    escoreRisco: null,
    observacaoMedico: null,
    registradoProntuarioEm: null,
    mensagemErro: null,
    solicitadaEm: '2026-11-04T13:30:00Z',
    concluidaEm: '2026-11-04T13:30:05Z',
    ...extra,
  };
}
