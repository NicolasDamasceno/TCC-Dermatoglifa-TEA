import type { DedoSessao, SessaoColeta, SituacaoDedo } from '../api/coletas';
import { DEDOS, MAOS } from '../dominio/dedos';
import { ID_PACIENTE } from './pacientes';

export const ID_SESSAO = '00000000-0000-0000-0000-0000000005e5';

/** Sessão sintética; `situacoes` na ordem da ficha (direita polegar → esquerda mínimo). */
export function sessaoColeta(extra: Partial<SessaoColeta> = {}, situacoes: SituacaoDedo[] = []): SessaoColeta {
  const dedos: DedoSessao[] = MAOS.flatMap((mao) => DEDOS.map((dedo) => ({ mao, dedo }))).map(({ mao, dedo }, i) => {
    const situacao = situacoes[i] ?? 'PENDENTE';
    return {
      mao,
      dedo,
      situacao,
      resolucaoDpi: situacao === 'COLETADO' ? 500 : null,
      motivoNaoColetavel: situacao === 'NAO_COLETAVEL' ? 'Lesão (sintético)' : null,
      capturadaEm: situacao === 'PENDENTE' ? null : '2026-11-04T13:05:00Z',
    };
  });

  return {
    id: ID_SESSAO,
    pacienteId: ID_PACIENTE,
    status: 'EM_ANDAMENTO',
    metodo: 'LEITOR_OPTICO',
    tipoImpressao: 'ROLADA',
    dispositivo: 'Leitor sintético',
    idadeMeses: 55,
    iniciadaEm: '2026-11-04T13:00:00Z',
    concluidaEm: null,
    dedos,
    analiseId: null,
    analiseStatus: null,
    ...extra,
  };
}

export const TODOS_COLETADOS: SituacaoDedo[] = Array<SituacaoDedo>(10).fill('COLETADO');

/** Arquivo de imagem sintético (bytes quaisquer: quem valida o conteúdo é a API). */
export function arquivo(nome = 'polegar.png', tipo = 'image/png', tamanho = 1024): File {
  return new File([new Uint8Array(tamanho)], nome, { type: tipo });
}
