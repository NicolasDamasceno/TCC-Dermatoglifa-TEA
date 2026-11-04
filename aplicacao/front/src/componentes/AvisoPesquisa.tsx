import estilos from './AvisoPesquisa.module.css';

/**
 * Faixa fixa em todas as telas (CLAUDE.md e doc 01): o sistema é protótipo de
 * pesquisa e a saída da IA é apoio à decisão do médico, nunca diagnóstico.
 */
export function AvisoPesquisa() {
  return (
    <aside className={estilos.faixa} aria-label="Aviso de pesquisa">
      <p className={estilos.texto}>
        <strong>Protótipo de pesquisa.</strong> Uso restrito ao protocolo aprovado pelo CEP. Não é ferramenta de
        diagnóstico: o diagnóstico de TEA é clínico.
      </p>
    </aside>
  );
}
