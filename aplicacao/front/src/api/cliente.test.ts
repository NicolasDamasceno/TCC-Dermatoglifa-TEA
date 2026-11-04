import { describe, expect, it, vi } from 'vitest';
import { json, simularFetch } from '../testes/utilitarios';
import { criarClienteApi } from './cliente';
import { ErroApi, ErroConexao, mensagemDeErro } from './erros';

function cliente(token: string | null = 'abc') {
  const aoPerderSessao = vi.fn();
  const api = criarClienteApi({ urlBase: '', obterToken: () => token, aoPerderSessao });
  return { api, aoPerderSessao };
}

function cabecalhos(fetchMock: ReturnType<typeof simularFetch>): Headers {
  return fetchMock.mock.calls[0]![1]!.headers as Headers;
}

describe('cliente da API', () => {
  it('envia JSON com o token e devolve o corpo', async () => {
    const fetchMock = simularFetch(json(200, { ok: true }));
    const { api } = cliente();

    const resposta = await api.requisitar<{ ok: boolean }>('POST', '/api/x', { a: 1 });

    expect(resposta).toEqual({ ok: true });
    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('/api/x');
    expect(init!.method).toBe('POST');
    expect(init!.body).toBe('{"a":1}');
    expect(cabecalhos(fetchMock).get('Authorization')).toBe('Bearer abc');
    expect(cabecalhos(fetchMock).get('Content-Type')).toBe('application/json');
  });

  it('não envia Authorization sem sessão', async () => {
    const fetchMock = simularFetch(json(200, {}));
    await cliente(null).api.requisitar('GET', '/api/x');
    expect(cabecalhos(fetchMock).has('Authorization')).toBe(false);
  });

  it('repassa FormData sem definir Content-Type (o navegador põe o boundary)', async () => {
    const fetchMock = simularFetch(new Response(null, { status: 204 }));
    const dados = new FormData();
    dados.append('imagem', new Blob(['sintetico'], { type: 'image/png' }), 'dedo.png');

    const resposta = await cliente().api.requisitar('PUT', '/api/sessoes/1/digitais/DIREITA/POLEGAR', dados);

    expect(resposta).toBeUndefined();
    expect(fetchMock.mock.calls[0]![1]!.body).toBe(dados);
    expect(cabecalhos(fetchMock).has('Content-Type')).toBe(false);
  });

  it('converte problem+json em ErroApi com erros por campo', async () => {
    simularFetch(
      json(400, { title: 'Dados inválidos', errors: { Email: ['Informe um e-mail válido.'] }, traceId: '00-abc' }),
    );

    const erro = await cliente().api.requisitar('POST', '/api/x', {}).catch((e: unknown) => e);

    expect(erro).toBeInstanceOf(ErroApi);
    const erroApi = erro as ErroApi;
    expect(erroApi.status).toBe(400);
    expect(erroApi.message).toBe('Dados inválidos');
    expect(erroApi.doCampo('email')).toBe('Informe um e-mail válido.');
    expect(erroApi.doCampo('senha')).toBeUndefined();
    expect(erroApi.traceId).toBe('00-abc');
  });

  it('usa mensagem padrão quando o corpo não é JSON', async () => {
    simularFetch(new Response('falha', { status: 502 }));
    const erro = await cliente().api.requisitar('GET', '/api/x').catch((e: unknown) => e);
    expect(mensagemDeErro(erro)).toBe('O servidor teve um problema. Tente de novo em instantes.');
  });

  it('avisa a perda da sessão em 401 de rota autenticada', async () => {
    simularFetch(json(401, { title: 'Autenticação necessária. Faça login novamente.' }));
    const { api, aoPerderSessao } = cliente('expirado');

    await expect(api.requisitar('GET', '/api/pacientes')).rejects.toBeInstanceOf(ErroApi);
    expect(aoPerderSessao).toHaveBeenCalledOnce();
  });

  it('não trata 401 do login (sem token) como perda de sessão', async () => {
    simularFetch(json(401, { title: 'E-mail ou senha incorretos.' }));
    const { api, aoPerderSessao } = cliente(null);

    await expect(api.requisitar('POST', '/api/auth/login', {})).rejects.toThrow('E-mail ou senha incorretos.');
    expect(aoPerderSessao).not.toHaveBeenCalled();
  });

  it('transforma falha de rede em ErroConexao', async () => {
    simularFetch(new TypeError('Failed to fetch'));
    await expect(cliente().api.requisitar('GET', '/api/x')).rejects.toBeInstanceOf(ErroConexao);
  });

  it('não expõe detalhes de erros desconhecidos', () => {
    expect(mensagemDeErro(new Error('stack interna'))).toBe('Algo deu errado. Tente de novo.');
  });
});
