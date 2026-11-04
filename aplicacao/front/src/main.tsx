import '@fontsource-variable/atkinson-hyperlegible-next';
import './estilos/tokens.css';
import './estilos/base.css';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import { App } from './App';
import { ErroApi } from './api/erros';
import { SessaoProvider } from './sessao/SessaoProvider';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Erros 4xx não melhoram com nova tentativa; só falhas de rede ou 5xx.
      retry: (tentativas, erro) => tentativas < 2 && !(erro instanceof ErroApi && erro.status < 500),
      refetchOnWindowFocus: false,
    },
  },
});

const raiz = document.getElementById('raiz');
if (!raiz) {
  throw new Error('Elemento #raiz não encontrado no index.html.');
}

createRoot(raiz).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <SessaoProvider>
          <App />
        </SessaoProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
