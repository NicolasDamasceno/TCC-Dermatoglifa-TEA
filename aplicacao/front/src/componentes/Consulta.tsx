import { mensagemDeErro } from '../api/erros';
import { Botao } from './Botao';
import { Mensagem } from './Mensagem';
import estilos from './Consulta.module.css';

/** Texto discreto no lugar do conteúdo (doc 09, seção 6: sem esqueletos animados). */
export function Carregando({ texto }: { texto: string }) {
  return (
    <p className={estilos.carregando} role="status">
      {texto}
    </p>
  );
}

/** Falha ao buscar dados: diz o que houve e oferece tentar de novo. */
export function FalhaAoCarregar({ erro, tentar }: { erro: unknown; tentar: () => void }) {
  return (
    <Mensagem tipo="erro">
      <p>{mensagemDeErro(erro)}</p>
      <Botao variante="secundario" className={estilos.tentar} onClick={tentar}>
        Tentar de novo
      </Botao>
    </Mensagem>
  );
}
