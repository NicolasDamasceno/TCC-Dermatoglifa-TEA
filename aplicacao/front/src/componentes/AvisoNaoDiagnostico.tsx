import estilos from './AvisoNaoDiagnostico.module.css';

/**
 * Aviso obrigatório em todo resultado (RN04, UC08). O texto vem da API, na versão
 * registrada com a análise, para que o médico leia exatamente o que foi guardado.
 */
export function AvisoNaoDiagnostico({ texto, versao }: { texto: string; versao: string }) {
  return (
    <section className={estilos.aviso} aria-labelledby="titulo-aviso">
      <h2 id="titulo-aviso" className={estilos.titulo}>
        Não é diagnóstico
      </h2>
      <p className={estilos.texto}>{texto}</p>
      <p className={estilos.versao}>Versão do aviso: {versao}</p>
    </section>
  );
}
