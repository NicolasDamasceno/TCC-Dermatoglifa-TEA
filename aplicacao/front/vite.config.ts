/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv, type Plugin } from 'vite';

/**
 * Content-Security-Policy só no build de produção (em desenvolvimento o Vite usa
 * scripts inline para recarregar a página). Tudo vem da própria origem; `blob:`
 * em img-src é a pré-visualização local das digitais (RNF13). `frame-ancestors`
 * não vale em <meta>: o servidor que publicar o front deve enviá-lo no cabeçalho
 * (doc 09, seção 5).
 */
function politicaDeConteudo(urlApi: string | undefined): Plugin {
  const origemApi = urlApi ? new URL(urlApi).origin : '';
  const politica = [
    "default-src 'self'",
    "script-src 'self'",
    "style-src 'self'",
    "img-src 'self' blob:",
    "font-src 'self'",
    `connect-src 'self'${origemApi ? ` ${origemApi}` : ''}`,
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
  ].join('; ');

  return {
    name: 'politica-de-conteudo',
    apply: 'build',
    transformIndexHtml: () => [
      { tag: 'meta', attrs: { 'http-equiv': 'Content-Security-Policy', content: politica }, injectTo: 'head-prepend' },
    ],
  };
}

// Em desenvolvimento (e no `npm run preview`), /api é repassado para a API .NET:
// o navegador só fala com a própria origem e não precisa de CORS (ADR-0007).
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_');
  const proxy = { '/api': { target: env.VITE_API_ALVO || 'http://localhost:5117', changeOrigin: true } };
  return {
    plugins: [react(), politicaDeConteudo(env.VITE_API_URL)],
    server: { port: 5173, strictPort: true, proxy },
    preview: { port: 4173, strictPort: true, proxy },
    build: { sourcemap: false },
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: ['./src/testes/setup.ts'],
      css: { modules: { classNameStrategy: 'non-scoped' } },
      restoreMocks: true,
    },
  };
});
