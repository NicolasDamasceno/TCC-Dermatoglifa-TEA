/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv } from 'vite';

// Em desenvolvimento, /api é repassado para a API .NET: o navegador só fala com a
// própria origem e não precisa de CORS (ADR-0007).
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_');
  return {
    plugins: [react()],
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        '/api': { target: env.VITE_API_ALVO || 'http://localhost:5117', changeOrigin: true },
      },
    },
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: ['./src/testes/setup.ts'],
      css: { modules: { classNameStrategy: 'non-scoped' } },
      restoreMocks: true,
    },
  };
});
