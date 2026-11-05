import { useId, type ReactNode } from 'react';
import estilos from './Opcoes.module.css';

interface Opcao<T extends string> {
  valor: T;
  rotulo: string;
  descricao?: string;
}

interface PropsOpcoes<T extends string> {
  legenda: string;
  opcoes: readonly Opcao<T>[];
  valor: T | '';
  aoMudar: (valor: T) => void;
  erro?: string | undefined;
  dica?: string;
  /** Lado a lado (opções curtas) ou empilhadas (opções com descrição). */
  emLinha?: boolean;
}

/** Grupo de rádios com legenda, dica e erro ligados ao grupo. */
export function Opcoes<T extends string>({ legenda, opcoes, valor, aoMudar, erro, dica, emLinha = false }: PropsOpcoes<T>) {
  const id = useId();
  const descritores = [dica ? `${id}-dica` : '', erro ? `${id}-erro` : ''].filter(Boolean).join(' ') || undefined;

  return (
    <fieldset className={estilos.grupo} aria-describedby={descritores} aria-invalid={erro ? true : undefined}>
      <legend className={estilos.legenda}>{legenda}</legend>
      {dica && (
        <p id={`${id}-dica`} className={estilos.dica}>
          {dica}
        </p>
      )}
      <div className={emLinha ? estilos.emLinha : estilos.empilhadas}>
        {opcoes.map((opcao) => (
          <label key={opcao.valor} className={estilos.opcao}>
            <input
              type="radio"
              name={id}
              value={opcao.valor}
              checked={valor === opcao.valor}
              onChange={() => {
                aoMudar(opcao.valor);
              }}
            />
            <span>
              <span className={estilos.rotulo}>{opcao.rotulo}</span>
              {opcao.descricao && <span className={estilos.descricao}>{opcao.descricao}</span>}
            </span>
          </label>
        ))}
      </div>
      {erro && (
        <p id={`${id}-erro`} className={estilos.erro}>
          {erro}
        </p>
      )}
    </fieldset>
  );
}

interface PropsMarcacao {
  rotulo: ReactNode;
  descricao?: string;
  marcado: boolean;
  aoMudar: (marcado: boolean) => void;
  erro?: string | undefined;
}

/** Caixa de marcação com texto por extenso (as autorizações do TCLE). */
export function Marcacao({ rotulo, descricao, marcado, aoMudar, erro }: PropsMarcacao) {
  const id = useId();

  return (
    <div className={estilos.comErro}>
      <label className={`${estilos.opcao} ${estilos.marcacao}`}>
        <input
          type="checkbox"
          checked={marcado}
          aria-invalid={erro ? true : undefined}
          aria-describedby={erro ? `${id}-erro` : undefined}
          onChange={(e) => {
            aoMudar(e.target.checked);
          }}
        />
        <span>
          <span className={estilos.rotulo}>{rotulo}</span>
          {descricao && <span className={estilos.descricao}>{descricao}</span>}
        </span>
      </label>
      {erro && (
        <p id={`${id}-erro`} className={estilos.erro}>
          {erro}
        </p>
      )}
    </div>
  );
}
