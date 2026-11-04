import { describe, expect, it } from 'vitest';
import { MEDICO, respostaEntrada } from '../testes/utilitarios';
import { destinoSeguro, paginaInicialDo } from './destinos';
import { apagarSessao, CHAVE_SESSAO, guardarSessao, lerSessaoGuardada, sessaoDaResposta } from './sessao';

describe('sessão guardada na aba', () => {
  it('guarda e lê de volta a sessão válida', () => {
    const sessao = sessaoDaResposta(respostaEntrada());
    guardarSessao(sessao);
    expect(lerSessaoGuardada()).toEqual(sessao);
  });

  it('guarda só token, validade e usuário', () => {
    guardarSessao(sessaoDaResposta(respostaEntrada()));
    const bruto = JSON.parse(sessionStorage.getItem(CHAVE_SESSAO)!) as Record<string, unknown>;
    expect(Object.keys(bruto).sort()).toEqual(['expiraEm', 'token', 'usuario']);
  });

  it('descarta e apaga sessão expirada', () => {
    guardarSessao({ token: 't', expiraEm: Date.now() - 1, usuario: MEDICO });
    expect(lerSessaoGuardada()).toBeNull();
    expect(sessionStorage.getItem(CHAVE_SESSAO)).toBeNull();
  });

  it.each([
    ['JSON inválido', '{nao-e-json'],
    ['papel desconhecido', JSON.stringify({ token: 't', expiraEm: Date.now() + 1e6, usuario: { ...MEDICO, papel: 'ROOT' } })],
    ['sem token', JSON.stringify({ expiraEm: Date.now() + 1e6, usuario: MEDICO })],
  ])('descarta conteúdo adulterado: %s', (_caso, bruto) => {
    sessionStorage.setItem(CHAVE_SESSAO, bruto);
    expect(lerSessaoGuardada()).toBeNull();
    expect(sessionStorage.getItem(CHAVE_SESSAO)).toBeNull();
  });

  it('apaga a sessão ao sair', () => {
    guardarSessao(sessaoDaResposta(respostaEntrada()));
    apagarSessao();
    expect(lerSessaoGuardada()).toBeNull();
  });
});

describe('destinos', () => {
  it('leva cada papel à sua tela', () => {
    expect(paginaInicialDo('MEDICO')).toBe('/pacientes');
    expect(paginaInicialDo('ADMIN')).toBe('/admin/solicitacoes');
  });

  it.each([
    ['/pacientes/1?aba=sessoes', '/pacientes/1?aba=sessoes'],
    ['//site-externo.example', undefined],
    ['https://site-externo.example', undefined],
    ['/entrar', undefined],
    [undefined, undefined],
    [42, undefined],
  ])('destinoSeguro(%s) = %s', (de, esperado) => {
    expect(destinoSeguro(de)).toBe(esperado);
  });
});
