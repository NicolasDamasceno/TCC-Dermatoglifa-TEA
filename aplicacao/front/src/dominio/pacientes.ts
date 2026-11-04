import type {
  MetodoCaptura,
  Sexo,
  Situacao,
  StatusAnalise,
  StatusSessao,
  TipoImpressao,
  TipoTermo,
} from '../api/pacientes';

export const NOME_SEXO: Readonly<Record<Sexo, string>> = { MASCULINO: 'Masculino', FEMININO: 'Feminino' };

export const NOME_SITUACAO: Readonly<Record<Situacao, string>> = {
  SUSPEITA: 'Suspeita de TEA',
  TEA_CONFIRMADO: 'TEA confirmado',
  TEA_DESCARTADO: 'TEA descartado',
  CONTROLE: 'Controle',
};

export const NOME_TERMO: Readonly<Record<TipoTermo, string>> = { TCLE: 'TCLE', TALE: 'TALE' };

export const NOME_STATUS_SESSAO: Readonly<Record<StatusSessao, string>> = {
  EM_ANDAMENTO: 'Em andamento',
  COMPLETA: 'Completa',
  INCOMPLETA: 'Incompleta',
  CANCELADA: 'Cancelada',
};

export const NOME_STATUS_ANALISE: Readonly<Record<StatusAnalise, string>> = {
  PENDENTE: 'Aguardando',
  PROCESSANDO: 'Em processamento',
  CONCLUIDA: 'Concluída',
  ERRO: 'Com erro',
};

export const NOME_METODO: Readonly<Record<MetodoCaptura, string>> = {
  LEITOR_OPTICO: 'Leitor óptico',
  TINTA_DIGITALIZADA: 'Tinta e papel, digitalizada',
};

export const NOME_IMPRESSAO: Readonly<Record<TipoImpressao, string>> = { ROLADA: 'Rolada', PLANA: 'Plana' };

/** Parentescos oferecidos na tela; a API guarda texto livre (até 50), então valores antigos aparecem como vieram. */
export const PARENTESCOS: readonly { valor: string; nome: string }[] = [
  { valor: 'MAE', nome: 'Mãe' },
  { valor: 'PAI', nome: 'Pai' },
  { valor: 'AVO', nome: 'Avó ou avô' },
  { valor: 'TUTOR', nome: 'Tutor ou tutora legal' },
  { valor: 'OUTRO', nome: 'Outro' },
];

export function nomeParentesco(valor: string | null): string {
  if (!valor) return '—';
  return PARENTESCOS.find((p) => p.valor === valor)?.nome ?? valor;
}

/** Data de hoje no fuso do navegador, em AAAA-MM-DD (formato da API e do input date). */
export function hojeIso(agora: Date = new Date()): string {
  const m = String(agora.getMonth() + 1).padStart(2, '0');
  const d = String(agora.getDate()).padStart(2, '0');
  return `${agora.getFullYear()}-${m}-${d}`;
}

function partes(iso: string): [number, number, number] | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  return m ? [Number(m[1]), Number(m[2]), Number(m[3])] : null;
}

/** Idade em meses completos (mesma regra de IdentificacaoPaciente.IdadeEmMeses no back). */
export function idadeEmMeses(nascimento: string, hoje: string = hojeIso()): number | null {
  const n = partes(nascimento);
  const h = partes(hoje);
  if (!n || !h) return null;
  let meses = (h[0] - n[0]) * 12 + h[1] - n[1];
  if (h[2] < n[2]) meses -= 1;
  return meses;
}

/** "3 anos e 2 meses" até 6 anos; a partir daí, só os anos. */
export function idadePorExtenso(nascimento: string | null, hoje: string = hojeIso()): string {
  const meses = nascimento ? idadeEmMeses(nascimento, hoje) : null;
  if (meses === null || meses < 0) return '—';
  const anos = Math.floor(meses / 12);
  const resto = meses % 12;
  const textoAnos = anos === 1 ? '1 ano' : `${anos} anos`;
  const textoMeses = resto === 1 ? '1 mês' : `${resto} meses`;
  if (anos === 0) return textoMeses;
  if (anos >= 6 || resto === 0) return textoAnos;
  return `${textoAnos} e ${textoMeses}`;
}

export function ehMenorDeIdade(nascimento: string, hoje: string = hojeIso()): boolean {
  const meses = idadeEmMeses(nascimento, hoje);
  return meses !== null && meses < 18 * 12;
}
