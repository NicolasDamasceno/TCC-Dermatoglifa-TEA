import { ErroApi, ErroConexao } from './erros';

export type MetodoHttp = 'GET' | 'POST' | 'PUT' | 'PATCH';

export interface OpcoesCliente {
  /** Prefixo das rotas; vazio = mesma origem (o Vite repassa /api em desenvolvimento). */
  urlBase: string;
  obterToken: () => string | null;
  /** Chamado em qualquer 401 de rota autenticada: a sessão acabou. */
  aoPerderSessao: () => void;
}

export interface ClienteApi {
  requisitar<T>(metodo: MetodoHttp, caminho: string, corpo?: unknown): Promise<T>;
  /** GET de um arquivo (ex.: exportação da UC12): o conteúdo e o nome sugerido pela API. */
  baixar(caminho: string): Promise<ArquivoBaixado>;
}

export interface ArquivoBaixado {
  nome: string;
  conteudo: Blob;
}

/** Nome do arquivo no cabeçalho Content-Disposition (forma simples, `filename=`). */
function nomeDoArquivo(resposta: Response, padrao: string): string {
  const disposicao = resposta.headers.get('Content-Disposition') ?? '';
  const m = /filename="?([^";]+)"?/i.exec(disposicao);
  return m?.[1] ?? padrao;
}

interface Problema {
  title?: unknown;
  errors?: unknown;
  traceId?: unknown;
}

/**
 * Cliente HTTP da API. Envia JSON (ou FormData, para imagens), coloca o token,
 * e converte qualquer erro em {@link ErroApi} ou {@link ErroConexao}.
 */
export function criarClienteApi({ urlBase, obterToken, aoPerderSessao }: OpcoesCliente): ClienteApi {
  async function enviar(metodo: MetodoHttp, caminho: string, corpo?: unknown): Promise<Response> {
    const cabecalhos = new Headers({ Accept: 'application/json, application/problem+json, application/zip' });
    const token = obterToken();
    if (token) {
      cabecalhos.set('Authorization', `Bearer ${token}`);
    }

    let conteudo: BodyInit | undefined;
    if (corpo instanceof FormData) {
      conteudo = corpo; // o navegador define o boundary do multipart
    } else if (corpo !== undefined) {
      cabecalhos.set('Content-Type', 'application/json');
      conteudo = JSON.stringify(corpo);
    }

    let resposta: Response;
    try {
      resposta = await fetch(`${urlBase}${caminho}`, { method: metodo, headers: cabecalhos, body: conteudo });
    } catch {
      throw new ErroConexao();
    }

    if (resposta.status === 401 && token) {
      aoPerderSessao();
    }

    if (!resposta.ok) {
      throw await lerProblema(resposta);
    }

    return resposta;
  }

  return {
    async requisitar<T>(metodo: MetodoHttp, caminho: string, corpo?: unknown): Promise<T> {
      const resposta = await enviar(metodo, caminho, corpo);
      if (resposta.status === 204) {
        return undefined as T;
      }

      return (await resposta.json()) as T;
    },

    async baixar(caminho: string): Promise<ArquivoBaixado> {
      const resposta = await enviar('GET', caminho);
      return { nome: nomeDoArquivo(resposta, 'arquivo'), conteudo: await resposta.blob() };
    },
  };
}

async function lerProblema(resposta: Response): Promise<ErroApi> {
  let problema: Problema = {};
  try {
    problema = (await resposta.json()) as Problema;
  } catch {
    // corpo vazio ou que não é JSON: fica a mensagem padrão abaixo
  }

  const titulo = typeof problema.title === 'string' && problema.title ? problema.title : mensagemPadrao(resposta.status);
  const errosPorCampo = ehErrosPorCampo(problema.errors) ? problema.errors : {};
  const traceId = typeof problema.traceId === 'string' ? problema.traceId : undefined;
  return new ErroApi(resposta.status, titulo, errosPorCampo, traceId);
}

function ehErrosPorCampo(valor: unknown): valor is Record<string, string[]> {
  return (
    typeof valor === 'object' &&
    valor !== null &&
    Object.values(valor).every((v) => Array.isArray(v) && v.every((m) => typeof m === 'string'))
  );
}

function mensagemPadrao(status: number): string {
  if (status === 401) return 'Sua sessão terminou. Entre de novo.';
  if (status === 403) return 'Acesso não permitido para o seu perfil.';
  if (status === 404) return 'Não encontrado.';
  if (status >= 500) return 'O servidor teve um problema. Tente de novo em instantes.';
  return 'Não foi possível concluir a operação.';
}
