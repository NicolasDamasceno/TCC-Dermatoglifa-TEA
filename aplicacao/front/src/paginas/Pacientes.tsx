import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useState, type SubmitEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { chavesPacientes, listarPacientes, type PacienteResumo } from '../api/pacientes';
import { Botao } from '../componentes/Botao';
import { Campo } from '../componentes/Campo';
import { Carregando, FalhaAoCarregar } from '../componentes/Consulta';
import { LinkBotao } from '../componentes/LinkBotao';
import { useTituloPagina } from '../componentes/useTituloPagina';
import { idadePorExtenso, NOME_SEXO, NOME_SITUACAO } from '../dominio/pacientes';
import { useSessao } from '../sessao/contextoSessao';
import estilos from './Pacientes.module.css';

/** UC04 — lista e busca dos pacientes do médico (só os dele, RN05). */
export function Pacientes() {
  useTituloPagina('Pacientes');
  const { api } = useSessao();
  const [parametros, setParametros] = useSearchParams();
  const busca = parametros.get('busca')?.trim() ?? '';
  const [texto, setTexto] = useState(busca);

  const pacientes = useQuery({
    queryKey: chavesPacientes.lista(busca),
    queryFn: () => listarPacientes(api, busca),
    placeholderData: keepPreviousData,
  });

  function buscar(evento: SubmitEvent<HTMLFormElement>) {
    evento.preventDefault();
    const termo = texto.trim();
    setParametros(termo ? { busca: termo } : {});
  }

  function limpar() {
    setTexto('');
    setParametros({});
  }

  return (
    <section className={estilos.pagina} aria-labelledby="titulo-pacientes">
      <div className={estilos.cabecalho}>
        <h1 id="titulo-pacientes">Pacientes</h1>
        <LinkBotao to="/pacientes/novo">Cadastrar paciente</LinkBotao>
      </div>

      <form role="search" onSubmit={buscar} className={estilos.busca}>
        <Campo
          rotulo="Buscar por nome"
          type="search"
          name="busca"
          maxLength={100}
          value={texto}
          onChange={(e) => {
            setTexto(e.target.value);
          }}
          className={estilos.campoBusca}
        />
        <Botao type="submit" variante="secundario">
          Buscar
        </Botao>
      </form>

      {pacientes.isPending && <Carregando texto="Carregando pacientes…" />}
      {pacientes.isError && <FalhaAoCarregar erro={pacientes.error} tentar={() => void pacientes.refetch()} />}
      {pacientes.isSuccess && (
        <Resultado pacientes={pacientes.data} busca={busca} atualizando={pacientes.isPlaceholderData} limpar={limpar} />
      )}
    </section>
  );
}

interface PropsResultado {
  pacientes: PacienteResumo[];
  busca: string;
  atualizando: boolean;
  limpar: () => void;
}

function Resultado({ pacientes, busca, atualizando, limpar }: PropsResultado) {
  if (pacientes.length === 0 && !busca) {
    return (
      <div className={estilos.vazio}>
        <p>Nenhum paciente cadastrado ainda.</p>
        <p>Depois do cadastro, registre o TCLE assinado para liberar a coleta das digitais.</p>
        <LinkBotao to="/pacientes/novo" variante="secundario">
          Cadastrar o primeiro paciente
        </LinkBotao>
      </div>
    );
  }

  if (pacientes.length === 0) {
    return (
      <div className={estilos.vazio}>
        <p>Nenhum paciente com “{busca}” no nome.</p>
        <Botao variante="secundario" onClick={limpar}>
          Limpar busca
        </Botao>
      </div>
    );
  }

  const quantidade = pacientes.length === 1 ? '1 paciente' : `${pacientes.length} pacientes`;
  return (
    <div className={estilos.rolagem} role="region" aria-labelledby="legenda-pacientes" tabIndex={0}>
      <table className={estilos.tabela} aria-busy={atualizando || undefined}>
        <caption id="legenda-pacientes" className={estilos.legenda}>
          {busca ? `${quantidade} com “${busca}” no nome` : `${quantidade}, do cadastro mais recente ao mais antigo`}
        </caption>
        <thead>
          <tr>
            <th scope="col">Nome</th>
            <th scope="col">Idade</th>
            <th scope="col">Sexo</th>
            <th scope="col">Situação</th>
            <th scope="col">Coleta</th>
          </tr>
        </thead>
        <tbody>
          {pacientes.map((p) => (
            <tr key={p.id}>
              <th scope="row">
                <Link to={`/pacientes/${p.id}`} className={estilos.nome}>
                  {p.nome ?? 'Dados eliminados a pedido do titular'}
                </Link>
              </th>
              <td>{p.anonimizado ? '—' : idadePorExtenso(p.dataNascimento)}</td>
              <td>{NOME_SEXO[p.sexo]}</td>
              <td>{NOME_SITUACAO[p.situacao]}</td>
              <td>
                {p.podeColetar ? (
                  <span className={estilos.liberada}>Liberada</span>
                ) : (
                  <span className={estilos.bloqueada}>{p.anonimizado ? 'Indisponível' : 'Falta TCLE'}</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
