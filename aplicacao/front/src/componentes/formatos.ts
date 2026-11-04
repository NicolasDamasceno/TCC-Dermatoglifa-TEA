const DATA_HORA = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

/** "04/11/2026, 15:25" no fuso do navegador. */
export function dataHora(iso: string): string {
  const data = new Date(iso);
  return Number.isNaN(data.getTime()) ? '—' : DATA_HORA.format(data);
}
