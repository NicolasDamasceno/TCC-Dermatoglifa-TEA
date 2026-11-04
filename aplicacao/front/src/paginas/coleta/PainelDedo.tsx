import { useMutation } from '@tanstack/react-query';
import { useEffect, useId, useRef, useState, type ChangeEvent } from 'react';
import { enviarDigital, marcarNaoColetavel, type DedoSessao, type SessaoColeta } from '../../api/coletas';
import { mensagemDeErro } from '../../api/erros';
import { Botao } from '../../componentes/Botao';
import { CampoTextoLongo } from '../../componentes/Campo';
import { dataHora } from '../../componentes/formatos';
import { Mensagem } from '../../componentes/Mensagem';
import { FORMATOS_ACEITOS, nomeDoDedo, problemaDoArquivo, temPrevisualizacao } from '../../dominio/dedos';
import { useSessao } from '../../sessao/contextoSessao';
import estilos from './Coleta.module.css';

interface PropsPainel {
  sessaoId: string;
  dedo: DedoSessao;
  /** Chamado com a sessão atualizada; `miniatura` passa a ser da tela (não é revogada aqui). */
  aposResolver: (sessao: SessaoColeta, texto: string, miniatura?: string) => void;
  /** Depois de resolver o dedo anterior, o foco vem para este painel (o botão usado sumiu). */
  focarAoAbrir: boolean;
}

interface Escolha {
  arquivo: File;
  /** URL local (blob:) para a pré-visualização; nunca sai do navegador. */
  url: string | null;
}

/**
 * O dedo da vez: escolher o arquivo, conferir a pré-visualização e só então
 * enviar (RNF13, ADR-0004: evitar trocar a imagem de um dedo por outro).
 */
export function PainelDedo({ sessaoId, dedo, aposResolver, focarAoAbrir }: PropsPainel) {
  const { api } = useSessao();
  const nome = nomeDoDedo(dedo.mao, dedo.dedo);
  const nomeMaiusculo = nome.charAt(0).toUpperCase() + nome.slice(1);
  const idArquivo = useId();
  const refArquivo = useRef<HTMLInputElement>(null);
  const refTitulo = useRef<HTMLHeadingElement>(null);

  const [escolha, setEscolha] = useState<Escolha | null>(null);
  const [erroArquivo, setErroArquivo] = useState<string>();
  const [naoColetavel, setNaoColetavel] = useState(false);
  const [motivo, setMotivo] = useState('');
  const [erroMotivo, setErroMotivo] = useState<string>();

  useEffect(() => {
    if (focarAoAbrir) refTitulo.current?.focus();
  }, [focarAoAbrir]);

  // A URL local é liberada ao trocar de arquivo ou de dedo, a menos que tenha virado miniatura.
  const entregue = useRef<string | null>(null);
  useEffect(
    () => () => {
      if (escolha?.url && escolha.url !== entregue.current) URL.revokeObjectURL(escolha.url);
    },
    [escolha],
  );

  const envio = useMutation({
    mutationFn: (arquivo: File) => enviarDigital(api, sessaoId, dedo.mao, dedo.dedo, arquivo),
    onSuccess: (sessao) => {
      const url = escolha?.url ?? undefined;
      entregue.current = url ?? null;
      aposResolver(sessao, `${nomeMaiusculo} enviado.`, url);
    },
  });

  const registroNaoColetavel = useMutation({
    mutationFn: () => marcarNaoColetavel(api, sessaoId, dedo.mao, dedo.dedo, motivo.trim()),
    onSuccess: (sessao) => {
      aposResolver(sessao, `${nomeMaiusculo} registrado como não coletável.`);
    },
  });

  function escolher(evento: ChangeEvent<HTMLInputElement>) {
    const arquivo = evento.target.files?.[0];
    evento.target.value = ''; // permite escolher o mesmo arquivo de novo depois
    envio.reset();
    if (!arquivo) return;

    const problema = problemaDoArquivo(arquivo);
    setErroArquivo(problema);
    if (problema) {
      setEscolha(null);
      return;
    }

    setEscolha({ arquivo, url: temPrevisualizacao(arquivo) ? URL.createObjectURL(arquivo) : null });
  }

  function registrarNaoColetavel() {
    if (!motivo.trim()) {
      setErroMotivo('Informe o motivo.');
      return;
    }

    setErroMotivo(undefined);
    registroNaoColetavel.mutate();
  }

  return (
    <section className={estilos.painel} aria-labelledby="titulo-dedo">
      <h2 id="titulo-dedo" ref={refTitulo} tabIndex={-1}>
        {nomeMaiusculo}
      </h2>

      {dedo.situacao === 'COLETADO' && (
        <p className={estilos.nota}>
          Coletado em {dedo.capturadaEm ? dataHora(dedo.capturadaEm) : '—'}
          {dedo.resolucaoDpi ? `, ${dedo.resolucaoDpi} ppi` : ''}. Enviar outro arquivo substitui o atual.
        </p>
      )}
      {dedo.situacao === 'NAO_COLETAVEL' && (
        <p className={estilos.nota}>
          Registrado como não coletável: {dedo.motivoNaoColetavel}. Enviar uma imagem substitui este registro.
        </p>
      )}

      <div className={estilos.envio}>
        <div className={estilos.escolha}>
          <label htmlFor={idArquivo} className={estilos.rotuloArquivo}>
            Imagem do {nome}
          </label>
          <p className={estilos.nota} id={`${idArquivo}-dica`}>
            PNG, BMP ou TIFF sem perda, com pelo menos 500 ppi e até 5 MB.
          </p>
          <input
            ref={refArquivo}
            id={idArquivo}
            type="file"
            accept={FORMATOS_ACEITOS}
            className={estilos.arquivo}
            aria-describedby={[`${idArquivo}-dica`, erroArquivo ? `${idArquivo}-erro` : ''].filter(Boolean).join(' ')}
            aria-invalid={erroArquivo ? true : undefined}
            onChange={escolher}
          />
          {erroArquivo && (
            <p id={`${idArquivo}-erro`} className={estilos.erro}>
              {erroArquivo}
            </p>
          )}
        </div>

        {escolha && (
          <figure className={estilos.previa}>
            {escolha.url ? (
              <img src={escolha.url} alt={`Pré-visualização da imagem escolhida para o ${nome}`} />
            ) : (
              <p className={estilos.semPrevia}>O navegador não mostra arquivos TIFF. Confira pelo nome do arquivo.</p>
            )}
            <figcaption>
              <span className={estilos.nomeArquivo}>{escolha.arquivo.name}</span>
              <strong>Confira: esta é a digital do {nome}?</strong>
            </figcaption>
          </figure>
        )}
      </div>

      {envio.error && <Mensagem tipo="erro">{mensagemDeErro(envio.error)}</Mensagem>}

      {escolha && (
        <div className={estilos.acoes}>
          <Botao
            carregando={envio.isPending}
            textoCarregando="Enviando…"
            onClick={() => {
              envio.mutate(escolha.arquivo);
            }}
          >
            Enviar {nome}
          </Botao>
          <Botao
            variante="secundario"
            onClick={() => {
              refArquivo.current?.click();
            }}
          >
            Escolher outro arquivo
          </Botao>
        </div>
      )}

      {dedo.situacao !== 'NAO_COLETAVEL' && (
        <div className={estilos.naoColetavel}>
          {!naoColetavel ? (
            <Botao
              variante="secundario"
              onClick={() => {
                setNaoColetavel(true);
              }}
            >
              Este dedo não pode ser coletado
            </Botao>
          ) : (
            <div className={estilos.formNaoColetavel} role="group" aria-label={`Registrar ${nome} como não coletável`}>
              {registroNaoColetavel.error && <Mensagem tipo="erro">{mensagemDeErro(registroNaoColetavel.error)}</Mensagem>}
              <CampoTextoLongo
                rotulo="Motivo"
                dica="Por exemplo: amputação, lesão ou malformação. Com um dedo não coletável, a coleta não é analisada."
                maxLength={200}
                rows={2}
                value={motivo}
                onChange={(e) => {
                  setMotivo(e.target.value);
                }}
                erro={erroMotivo}
              />
              <div className={estilos.acoes}>
                <Botao
                  carregando={registroNaoColetavel.isPending}
                  textoCarregando="Registrando…"
                  onClick={registrarNaoColetavel}
                >
                  Registrar como não coletável
                </Botao>
                <Botao
                  variante="secundario"
                  onClick={() => {
                    setNaoColetavel(false);
                    setMotivo('');
                    setErroMotivo(undefined);
                  }}
                >
                  Voltar
                </Botao>
              </div>
            </div>
          )}
        </div>
      )}
    </section>
  );
}
