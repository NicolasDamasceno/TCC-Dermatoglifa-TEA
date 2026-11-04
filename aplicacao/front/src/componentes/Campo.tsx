import { useId, type InputHTMLAttributes } from 'react';
import estilos from './Campo.module.css';

interface PropsCampo extends Omit<InputHTMLAttributes<HTMLInputElement>, 'id'> {
  rotulo: string;
  /** Mensagem de erro do campo (vinda da API ou da validação local). */
  erro?: string | undefined;
  dica?: string;
}

/** Rótulo sempre visível acima do campo; erro e dica ligados por aria-describedby. */
export function Campo({ rotulo, erro, dica, className, ...resto }: PropsCampo) {
  const id = useId();
  const idDica = dica ? `${id}-dica` : undefined;
  const idErro = erro ? `${id}-erro` : undefined;
  const descritores = [idDica, idErro].filter(Boolean).join(' ') || undefined;

  return (
    <div className={[estilos.campo, className].filter(Boolean).join(' ')}>
      <label htmlFor={id} className={estilos.rotulo}>
        {rotulo}
      </label>
      {dica && (
        <p id={idDica} className={estilos.dica}>
          {dica}
        </p>
      )}
      <input
        {...resto}
        id={id}
        className={estilos.entrada}
        aria-invalid={erro ? true : undefined}
        aria-describedby={descritores}
      />
      {erro && (
        <p id={idErro} className={estilos.erro}>
          {erro}
        </p>
      )}
    </div>
  );
}
