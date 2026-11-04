import { useEffect } from 'react';

/** Título da aba por tela (WCAG 2.4.2): "Pacientes | Dermatoglifia TEA". */
export function useTituloPagina(titulo: string): void {
  useEffect(() => {
    document.title = `${titulo} | Dermatoglifia TEA`;
  }, [titulo]);
}
