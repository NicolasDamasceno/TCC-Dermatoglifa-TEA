import { useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import estilos from './Campo.module.css';

interface PropsComuns {
  rotulo: string;
  /** Mensagem de erro do campo (vinda da API ou da validação local). */
  erro?: string | undefined;
  dica?: string;
  className?: string;
}

interface Ligacao {
  id: string;
  'aria-invalid': true | undefined;
  'aria-describedby': string | undefined;
}

/** Rótulo sempre visível acima do campo; dica e erro ligados por aria-describedby. */
function Moldura({ rotulo, erro, dica, className, controle }: PropsComuns & { controle: (ligacao: Ligacao) => ReactNode }) {
  const id = useId();
  const idDica = dica ? `${id}-dica` : undefined;
  const idErro = erro ? `${id}-erro` : undefined;
  const ligacao: Ligacao = {
    id,
    'aria-invalid': erro ? true : undefined,
    'aria-describedby': [idDica, idErro].filter(Boolean).join(' ') || undefined,
  };

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
      {controle(ligacao)}
      {erro && (
        <p id={idErro} className={estilos.erro}>
          {erro}
        </p>
      )}
    </div>
  );
}

type PropsCampo = PropsComuns & Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'className'>;

export function Campo({ rotulo, erro, dica, className, ...resto }: PropsCampo) {
  return (
    <Moldura
      rotulo={rotulo}
      erro={erro}
      dica={dica}
      className={className}
      controle={(ligacao) => <input {...resto} {...ligacao} className={estilos.entrada} />}
    />
  );
}

type PropsSelecao = PropsComuns & Omit<SelectHTMLAttributes<HTMLSelectElement>, 'id' | 'className'>;

export function CampoSelecao({ rotulo, erro, dica, className, children, ...resto }: PropsSelecao) {
  return (
    <Moldura
      rotulo={rotulo}
      erro={erro}
      dica={dica}
      className={className}
      controle={(ligacao) => (
        <select {...resto} {...ligacao} className={estilos.entrada}>
          {children}
        </select>
      )}
    />
  );
}

type PropsTextoLongo = PropsComuns & Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'id' | 'className'>;

export function CampoTextoLongo({ rotulo, erro, dica, className, ...resto }: PropsTextoLongo) {
  return (
    <Moldura
      rotulo={rotulo}
      erro={erro}
      dica={dica}
      className={className}
      controle={(ligacao) => <textarea rows={3} {...resto} {...ligacao} className={estilos.entrada} />}
    />
  );
}
