import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

// O jsdom não cria URLs blob: para a pré-visualização local das digitais.
let contadorBlob = 0;
URL.createObjectURL = () => `blob:teste/${String(++contadorBlob)}`;
URL.revokeObjectURL = () => undefined;

afterEach(() => {
  cleanup();
  sessionStorage.clear();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});
