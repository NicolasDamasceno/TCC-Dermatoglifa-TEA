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

/** "polegar direito", "médio esquerdo": para frases como "Enviar polegar direito". */
export function nomeDoDedo(mao: Mao, dedo: Dedo): string {
  const lado = mao === 'DIREITA' ? 'direito' : 'esquerdo';
  return `${NOME_DEDO[dedo].toLowerCase()} ${lado}`;
}

export function chaveDedo(mao: Mao, dedo: Dedo): string {
  return `${mao}-${dedo}`;
}

/** Regras do envio no back (ADR-0004): sem perda, até 5 MB. A resolução só a API confere. */
export const FORMATOS_ACEITOS = '.png,.bmp,.tif,.tiff,image/png,image/bmp,image/tiff';
export const TAMANHO_MAXIMO = 5 * 1024 * 1024;

export function problemaDoArquivo(arquivo: File): string | undefined {
  const nome = arquivo.name.toLowerCase();
  const extensaoOk = /\.(png|bmp|tiff?)$/.test(nome);
  const tipoOk = ['image/png', 'image/bmp', 'image/x-ms-bmp', 'image/tiff', ''].includes(arquivo.type);
  if (!extensaoOk || !tipoOk) return 'Formato não aceito. Envie PNG, BMP ou TIFF (sem perda; JPEG não é aceito).';
  if (arquivo.size > TAMANHO_MAXIMO) return 'Arquivo acima de 5 MB.';
  if (arquivo.size === 0) return 'O arquivo está vazio.';
  return undefined;
}

/** Navegadores comuns não exibem TIFF; PNG e BMP têm pré-visualização. */
export function temPrevisualizacao(arquivo: File): boolean {
  return !/\.tiff?$/i.test(arquivo.name) && arquivo.type !== 'image/tiff';
}
