import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useState, type SubmitEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import {
  chavesAuditoria,
  consultarAuditoria,
  obterFiltrosAuditoria,
  type ConsultaAuditoria,
  type FiltrosAuditoria,
  type PaginaAuditoria,
  type RegistroAuditoria,
} from '../api/auditoria';
import { Botao } from '../componentes/Botao';
import { Campo, CampoSelecao } from '../componentes/Campo';
import { Carregando, FalhaAoCarregar } from '../componentes/Consulta';
import { dataHora } from '../componentes/formatos';
import { useTituloPagina } from '../componentes/useTituloPagina';
import { ehIdentificador, inicioDoDia, nomeDaAcao, nomeDaEntidade } from '../dominio/auditoria';
import { useSessao } from '../sessao/contextoSessao';
import estilos from './Auditoria.module.css';

const CAMPOS = ['usuario', 'paciente', 'acao', 'de', 'ate'] as const;
type Campo = (typeof CAMPOS)[number];
type Filtros = Record<Campo, string>;
type Erros = Partial<Record<Campo, string>>;

function lerFiltros(p: URLSearchParams): Filtros {
  return Object.fromEntries(CAMPOS.map((c) => [c, p.get(c) ?? ''])) as Filtros;
}

function problemas(f: Filtros): Erros {
  const erros: Erros = {};
  if (f.paciente && !ehIdentificador(f.paciente)) {
    erros.paciente = 'Use o identificador do paciente (36 caracteres, como aparece na trilha).';
  }
  if (f.de && f.ate && f.de > f.ate) erros.ate = 'O fim do período deve ser depois do início.';
  return erros;
}

/** UC11 — trilha de auditoria, só leitura, filtrada por usuário, paciente, ação e período. */
export function Auditoria() {
  useTituloPagina('Trilha de auditoria');
  const { api } = useSessao();
  const [parametros, setParametros] = useSearchParams();
  const aplicados = lerFiltros(parametros);
  const pagina = Math.max(1, Number(parametros.get('pagina')) || 1);
  const filtros = useQuery({ queryKey: chavesAuditoria.filtros, queryFn: () => obterFiltrosAuditoria(api) });
  const consulta: ConsultaAuditoria = {
    pagina,
    ...(aplicados.usuario && { usuarioId: aplicados.usuario }),
    ...(aplicados.paciente && ehIdentificador(aplicados.paciente) && { pacienteId: aplicados.paciente }),
    ...(aplicados.acao && { acao: aplicados.acao }),
    ...(aplicados.de && { de: inicioDoDia(aplicados.de) }),
    ...(aplicados.ate && { ate: inicioDoDia(aplicados.ate, true) }),
  };
  const eventos = useQuery({
    queryKey: chavesAuditoria.consulta(consulta),
    queryFn: () => consultarAuditoria(api, consulta),
    placeholderData: keepPreviousData,
  });

  function aplicar(novos: Filtros) {
    setParametros(Object.fromEntries(Object.entries(novos).filter(([, v]) => v)));
  }

  return (
    <section className={estilos.pagina} aria-labelledby="titulo-auditoria">
      <h1 id="titulo-auditoria">Trilha de auditoria</h1>
      <p className={estilos.nota}>
        Acessos e alterações registrados pelo sistema, do mais recente ao mais antigo. A trilha não guarda nomes de
        pacientes, imagens nem senhas: o paciente aparece pelo identificador. Toda consulta a esta tela também fica
        registrada.
      </p>

      <FormularioFiltros
        key={CAMPOS.map((c) => aplicados[c]).join('|')}
        inicial={aplicados}
        usuarios={filtros.data?.usuarios ?? []}
        acoes={filtros.data?.acoes ?? []}
        aplicar={aplicar}
      />

      {eventos.isPending && <Carregando texto="Carregando a trilha…" />}
      {eventos.isError && <FalhaAoCarregar erro={eventos.error} tentar={() => void eventos.refetch()} />}
      {eventos.isSuccess && (
        <Eventos pagina={eventos.data} atualizando={eventos.isPlaceholderData} parametros={parametros} />
      )}
    </section>
  );
}

interface PropsFormulario {
  inicial: Filtros;
  usuarios: FiltrosAuditoria['usuarios'];
  acoes: string[];
  aplicar: (filtros: Filtros) => void;
}

/** Recriado (pela `key`) sempre que os filtros do endereço mudam. */
function FormularioFiltros({ inicial, usuarios, acoes, aplicar }: PropsFormulario) {
  const [f, setF] = useState<Filtros>(inicial);
  const [erros, setErros] = useState<Erros>({});

  function alterar(campo: Campo, valor: string) {
    setF((atual) => ({ ...atual, [campo]: valor }));
    setErros((atuais) => Object.fromEntries(Object.entries(atuais).filter(([c]) => c !== campo)));
  }

  function enviar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    const limpos = Object.fromEntries(CAMPOS.map((c) => [c, f[c].trim()])) as Filtros;
    const novos = problemas(limpos);
    setErros(novos);
    if (Object.keys(novos).length === 0) aplicar(limpos);
  }

  function limpar() {
    setErros({});
    aplicar(lerFiltros(new URLSearchParams()));
  }

  return (
      <form onSubmit={enviar} noValidate className={estilos.filtros} aria-label="Filtrar a trilha">
        <CampoSelecao
          rotulo="Usuário"
          value={f.usuario}
          onChange={(e) => {
            alterar('usuario', e.target.value);
          }}
        >
          <option value="">Todos</option>
          {usuarios.map((u) => (
            <option key={u.id} value={u.id}>
              {u.nome} ({u.email}){u.papel === 'ADMIN' ? ', administrador' : ''}
            </option>
          ))}
        </CampoSelecao>
        <CampoSelecao
          rotulo="Ação"
          value={f.acao}
          onChange={(e) => {
            alterar('acao', e.target.value);
          }}
        >
          <option value="">Todas</option>
          {acoes.map((a) => (
            <option key={a} value={a}>
              {nomeDaAcao(a)}
            </option>
          ))}
        </CampoSelecao>
        <Campo
          rotulo="Paciente"
          dica="Identificador do paciente; inclui as coletas e as análises dele."
          spellCheck={false}
          autoComplete="off"
          value={f.paciente}
          onChange={(e) => {
            alterar('paciente', e.target.value);
          }}
          erro={erros.paciente}
        />
        <div className={estilos.periodo}>
          <Campo
            rotulo="De"
            type="date"
            value={f.de}
            onChange={(e) => {
              alterar('de', e.target.value);
            }}
          />
          <Campo
            rotulo="Até"
            type="date"
            value={f.ate}
            onChange={(e) => {
              alterar('ate', e.target.value);
            }}
            erro={erros.ate}
          />
        </div>
        <div className={estilos.acoes}>
          <Botao type="submit">Filtrar</Botao>
          <Botao variante="secundario" onClick={limpar}>
            Limpar filtros
          </Botao>
        </div>
      </form>
  );
}

function comPagina(parametros: URLSearchParams, pagina: number): string {
  const novos = new URLSearchParams(parametros);
  if (pagina === 1) novos.delete('pagina');
  else novos.set('pagina', String(pagina));
  const texto = novos.toString();
  return texto ? `?${texto}` : '?';
}

function Eventos({ pagina: p, atualizando, parametros }: { pagina: PaginaAuditoria; atualizando: boolean; parametros: URLSearchParams }) {
  if (p.total === 0) {
    return <p className={estilos.vazio}>Nenhum evento com esses filtros.</p>;
  }

  const paginas = Math.ceil(p.total / p.tamanho);
  const descricao = `${p.total} ${p.total === 1 ? 'evento' : 'eventos'}; página ${p.pagina} de ${paginas}`;

  return (
    <>
      <div className={estilos.rolagem} role="region" aria-label="Tabela de eventos" tabIndex={0} aria-busy={atualizando}>
        <table className={estilos.tabela}>
          <caption className={estilos.legenda}>{descricao}</caption>
          <thead>
            <tr>
              <th scope="col">Quando</th>
              <th scope="col">Usuário</th>
              <th scope="col">Ação</th>
              <th scope="col">Registro</th>
              <th scope="col">Detalhes</th>
              <th scope="col">IP</th>
            </tr>
          </thead>
          <tbody>
            {p.itens.map((r) => (
              <Linha key={r.id} registro={r} />
            ))}
          </tbody>
        </table>
      </div>
      {paginas > 1 && (
        <nav aria-label="Páginas da trilha" className={estilos.paginas}>
          {p.pagina > 1 ? <Link to={comPagina(parametros, p.pagina - 1)}>Eventos mais recentes</Link> : <span />}
          {p.pagina < paginas && <Link to={comPagina(parametros, p.pagina + 1)}>Eventos mais antigos</Link>}
        </nav>
      )}
    </>
  );
}

function Linha({ registro: r }: { registro: RegistroAuditoria }) {
  return (
    <tr>
      <th scope="row" className={estilos.quando}>
        {dataHora(r.ocorridoEm)}
      </th>
      <td>
        {r.usuario ? (
          <>
            {r.usuario.nome}
            <span className={estilos.secundario}>{r.usuario.email}</span>
          </>
        ) : (
          <span className={estilos.secundario}>sem usuário identificado</span>
        )}
      </td>
      <td>
        {nomeDaAcao(r.acao)}
        <span className={estilos.codigo}>{r.acao}</span>
      </td>
      <td>
        {nomeDaEntidade(r.entidade)}
        {r.entidadeId && <span className={estilos.codigo}>{r.entidadeId}</span>}
        {r.entidade === 'paciente' && r.entidadeId && (
          <Link
            to={`?paciente=${r.entidadeId}`}
            className={estilos.filtrarPaciente}
            aria-label={`Ver a trilha deste paciente (${r.entidadeId})`}
          >
            Ver a trilha deste paciente
          </Link>
        )}
      </td>
      <td>
        {r.detalhes ? (
          <ul className={estilos.detalhes}>
            {Object.entries(r.detalhes).map(([chave, valor]) => (
              <li key={chave}>
                {chave}: {valor}
              </li>
            ))}
          </ul>
        ) : (
          '—'
        )}
      </td>
      <td className={estilos.codigoCelula}>{r.ip ?? '—'}</td>
    </tr>
  );
}
