/**
 * Erro devolvido pela API em application/problem+json (doc 08, seção 5).
 * `message` é o `title`, já em português e pronto para exibir.
 */
export class ErroApi extends Error {
  readonly status: number;
  readonly errosPorCampo: Readonly<Record<string, readonly string[]>>;
  readonly traceId: string | undefined;

  constructor(status: number, titulo: string, errosPorCampo: Record<string, string[]> = {}, traceId?: string) {
    super(titulo);
    this.name = 'ErroApi';
    this.status = status;
    this.errosPorCampo = errosPorCampo;
    this.traceId = traceId;
  }

  /** Erros de um campo, procurando o nome sem diferenciar maiúsculas (a API usa PascalCase). */
  doCampo(campo: string): string | undefined {
    const chave = Object.keys(this.errosPorCampo).find((k) => k.toLowerCase() === campo.toLowerCase());
    return chave ? this.errosPorCampo[chave]?.join(' ') : undefined;
  }
}

/** A API não respondeu (fora do ar, sem rede). */
export class ErroConexao extends Error {
  constructor() {
    super('Não foi possível falar com o servidor. Verifique a conexão e tente de novo.');
    this.name = 'ErroConexao';
  }
}

/** Mensagem pronta para exibir a partir de qualquer erro. */
export function mensagemDeErro(erro: unknown): string {
  if (erro instanceof ErroApi || erro instanceof ErroConexao) {
    return erro.message;
  }

  return 'Algo deu errado. Tente de novo.';
}
