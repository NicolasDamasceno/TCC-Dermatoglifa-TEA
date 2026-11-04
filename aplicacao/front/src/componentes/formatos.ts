const DATA_HORA = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

/** "04/11/2026, 15:25" no fuso do navegador. */
export function dataHora(iso: string): string {
  const data = new Date(iso);
  return Number.isNaN(data.getTime()) ? '—' : DATA_HORA.format(data);
}

/** Data sem hora da API ("2026-11-03") → "03/11/2026", sem passar por fuso horário. */
export function data(iso: string | null): string {
  const m = iso ? /^(\d{4})-(\d{2})-(\d{2})/.exec(iso) : null;
  return m ? `${m[3]}/${m[2]}/${m[1]}` : '—';
}
