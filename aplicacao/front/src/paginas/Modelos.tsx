import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type SubmitEvent } from 'react';
import { mensagemDeErro } from '../api/erros';
import { CHAVE_MODELOS, obterModelos, promoverModelo, type ModeloIA, type ServicoIA } from '../api/modelos';
import { Botao } from '../componentes/Botao';
import { Campo } from '../componentes/Campo';
import { Carregando, FalhaAoCarregar } from '../componentes/Consulta';
import { data, dataHora } from '../componentes/formatos';
import { Mensagem } from '../componentes/Mensagem';
import { useTituloPagina } from '../componentes/useTituloPagina';
import { useSessao } from '../sessao/contextoSessao';
import estilos from './Modelos.module.css';

const NOME_ORIGEM = { SINTETICO: 'Sintéticos (demonstração)', REAL: 'Reais (pesquisa)' } as const;
const NOME_PASTA: Readonly<Record<string, string>> = { DEMO: 'demonstração', PRODUCAO: 'produção' };
const TAMANHO_MAXIMO = 1024 * 1024;

/** UC10 — o modelo que o Serviço de IA usa, as versões registradas e a promoção. */
export function Modelos() {
  useTituloPagina('Modelos de IA');
  const { api } = useSessao();
  const painel = useQuery({ queryKey: CHAVE_MODELOS, queryFn: () => obterModelos(api) });
  const [aviso, setAviso] = useState<string | null>(null);
  const refAviso = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (aviso) refAviso.current?.focus();
  }, [aviso]);

  return (
    <section className={estilos.pagina} aria-labelledby="titulo-modelos">
      <h1 id="titulo-modelos">Modelos de IA</h1>
      <p className={estilos.nota}>
        As análises só são aceitas da versão ativa. Para trocar de modelo, copie a pasta da versão para o Serviço de IA,
        reinicie-o e promova a versão aqui.
      </p>

      {aviso && (
        <div ref={refAviso} tabIndex={-1} className={estilos.foco}>
          <Mensagem>{aviso}</Mensagem>
        </div>
      )}

      {painel.isPending && <Carregando texto="Consultando o Serviço de IA…" />}
      {painel.isError && <FalhaAoCarregar erro={painel.error} tentar={() => void painel.refetch()} />}
      {painel.data && (
        <>
          <SecaoServico
            servico={painel.data.servico}
            promovido={(versao) => {
              setAviso(`Versão ${versao} promovida. As próximas análises usam este modelo.`);
            }}
          />
          <SecaoVersoes modelos={painel.data.modelos} />
        </>
      )}
    </section>
  );
}

function situacaoDoServico(s: ServicoIA): string {
  if (s.ativo) return 'Registrado e ativo. As análises usam esta versão.';
  if (!s.hashConfere) return 'Esta versão já foi registrada com outro arquivo. Treine com outro nome de versão.';
  if (s.registrado) return 'Registrado, mas inativo. As análises vão falhar até que ele seja promovido.';
  return 'Ainda não registrado. As análises vão falhar até que ele seja promovido.';
}

function SecaoServico({ servico, promovido }: { servico: ServicoIA; promovido: (versao: string) => void }) {
  const podePromover = servico.disponivel && !servico.ativo && servico.hashConfere;

  return (
    <section className={estilos.secao} aria-labelledby="titulo-servico">
      <h2 id="titulo-servico">Serviço de IA agora</h2>
      {!servico.disponivel ? (
        <Mensagem tipo="erro">
          {servico.motivo ?? 'O Serviço de IA não respondeu.'} Enquanto isso, as análises ficam com erro e podem ser
          repetidas depois.
        </Mensagem>
      ) : (
        <>
          <dl className={estilos.dados}>
            <div className={estilos.largo}>
              <dt>Versão carregada</dt>
              <dd>{servico.versaoModelo}</dd>
            </div>
            <div>
              <dt>Dados de treino</dt>
              <dd>{servico.origemDados ? NOME_ORIGEM[servico.origemDados] : '—'}</dd>
            </div>
            <div>
              <dt>Pasta do serviço</dt>
              <dd>{servico.pastaOrigem ? (NOME_PASTA[servico.pastaOrigem] ?? servico.pastaOrigem) : '—'}</dd>
            </div>
            <div className={estilos.largo}>
              <dt>SHA-256 do arquivo</dt>
              <dd className={estilos.hash}>{servico.sha256}</dd>
            </div>
          </dl>
          <p className={servico.ativo ? estilos.situacaoOk : estilos.situacaoAlerta}>{situacaoDoServico(servico)}</p>
          {podePromover && servico.versaoModelo && (
            <FormularioPromocao versao={servico.versaoModelo} sintetico={servico.origemDados === 'SINTETICO'} promovido={promovido} />
          )}
        </>
      )}
    </section>
  );
}

type Erros = Partial<Record<'config' | 'metricas', string>>;

function problemaDoJson(arquivo: File | null, nome: string): string | undefined {
  if (!arquivo) return `Escolha o ${nome}.`;
  if (!arquivo.name.toLowerCase().endsWith('.json')) return `Escolha um arquivo .json (o ${nome} da versão).`;
  if (arquivo.size > TAMANHO_MAXIMO) return `O ${nome} passa de 1 MB; confira se é o arquivo certo.`;
  return undefined;
}

function FormularioPromocao({
  versao,
  sintetico,
  promovido,
}: {
  versao: string;
  sintetico: boolean;
  promovido: (versao: string) => void;
}) {
  const { api } = useSessao();
  const queryClient = useQueryClient();
  const [config, setConfig] = useState<File | null>(null);
  const [metricas, setMetricas] = useState<File | null>(null);
  const [erros, setErros] = useState<Erros>({});

  const promocao = useMutation({
    mutationFn: ({ c, m }: { c: File; m: File }) => promoverModelo(api, c, m),
    onSuccess: (modelo) => {
      void queryClient.invalidateQueries({ queryKey: CHAVE_MODELOS });
      promovido(modelo.versao);
    },
  });

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    const novos: Erros = {};
    const problemaConfig = problemaDoJson(config, 'config.json');
    const problemaMetricas = problemaDoJson(metricas, 'metricas.json');
    if (problemaConfig) novos.config = problemaConfig;
    if (problemaMetricas) novos.metricas = problemaMetricas;
    setErros(novos);
    if (!config || !metricas || Object.keys(novos).length > 0) return;
    promocao.mutate({ c: config, m: metricas });
  }

  return (
    <form onSubmit={enviar} noValidate className={estilos.formulario} aria-label={`Promover a versão ${versao}`}>
      <h3>Promover esta versão</h3>
      <p className={estilos.nota}>
        Envie os dois arquivos da pasta da versão, em <code>treino/modelos/</code>. A versão e o hash são conferidos com
        o que o Serviço de IA carregou.
      </p>
      {sintetico && (
        <p className={estilos.situacaoAlerta}>
          Modelo treinado só com dados sintéticos. Os resultados não dizem nada sobre digitais reais e aparecem com esse
          alerta para o médico.
        </p>
      )}
      {promocao.error && <Mensagem tipo="erro">{mensagemDeErro(promocao.error)}</Mensagem>}

      <div className={estilos.linha}>
        <Campo
          rotulo="config.json"
          dica="Versão, arquitetura, origem dos dados e data do treino."
          type="file"
          accept=".json,application/json"
          onChange={(e) => {
            setConfig(e.target.files?.[0] ?? null);
            setErros((atual) => ({ ...atual, config: undefined }));
          }}
          erro={erros.config}
        />
        <Campo
          rotulo="metricas.json"
          dica="Métricas no conjunto de teste."
          type="file"
          accept=".json,application/json"
          onChange={(e) => {
            setMetricas(e.target.files?.[0] ?? null);
            setErros((atual) => ({ ...atual, metricas: undefined }));
          }}
          erro={erros.metricas}
        />
      </div>

      <div>
        <Botao type="submit" carregando={promocao.isPending} textoCarregando="Promovendo…">
          Promover {versao}
        </Botao>
      </div>
    </form>
  );
}

function SecaoVersoes({ modelos }: { modelos: ModeloIA[] }) {
  return (
    <section className={estilos.secao} aria-labelledby="titulo-versoes">
      <h2 id="titulo-versoes">Versões registradas</h2>
      {modelos.length === 0 ? (
        <p className={estilos.nota}>Nenhuma versão registrada.</p>
      ) : (
        <div className={estilos.rolagem} role="region" aria-label="Tabela de versões registradas" tabIndex={0}>
          <table className={estilos.tabela}>
            <thead>
              <tr>
                <th scope="col">Versão</th>
                <th scope="col">Dados de treino</th>
                <th scope="col">Treinado em</th>
                <th scope="col">Acurácia balanceada no teste</th>
                <th scope="col">Análises</th>
                <th scope="col">Situação</th>
              </tr>
            </thead>
            <tbody>
              {modelos.map((m) => (
                <tr key={m.id}>
                  <th scope="row">
                    <span className={estilos.versao}>{m.versao}</span>
                    <span className={estilos.arquitetura}>{m.arquitetura}</span>
                  </th>
                  <td>{NOME_ORIGEM[m.origemDados]}</td>
                  <td>{data(m.treinadoEm)}</td>
                  <td>
                    {m.metricas.acuraciaBalanceada === undefined
                      ? '—'
                      : m.metricas.acuraciaBalanceada.toLocaleString('pt-BR', { maximumFractionDigits: 3 })}
                    {m.metricas.nTeste !== undefined && <span className={estilos.arquitetura}>{m.metricas.nTeste} imagens</span>}
                  </td>
                  <td>{m.analises}</td>
                  <td>{m.ativo ? `Ativa desde ${m.promovidoEm ? dataHora(m.promovidoEm) : '—'}` : 'Inativa'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
