import type { DedoSessao } from '../../api/coletas';
import { chaveDedo } from '../../dominio/dedos';

/** Primeiro dedo pendente depois do atual, na ordem da ficha (volta ao início se preciso). */
export function proximoPendente(dedos: DedoSessao[], depoisDe?: string): DedoSessao | undefined {
  const inicio = depoisDe ? dedos.findIndex((d) => chaveDedo(d.mao, d.dedo) === depoisDe) + 1 : 0;
  const ordenados = [...dedos.slice(inicio), ...dedos.slice(0, inicio)];
  return ordenados.find((d) => d.situacao === 'PENDENTE');
}
