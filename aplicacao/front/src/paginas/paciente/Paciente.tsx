import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link, useLocation, useParams } from 'react-router';
import { ErroApi } from '../../api/erros';
import { chavesPacientes, obterPaciente } from '../../api/pacientes';
import { Carregando, FalhaAoCarregar } from '../../componentes/Consulta';
import { data } from '../../componentes/formatos';
import { Mensagem } from '../../componentes/Mensagem';
import { useTituloPagina } from '../../componentes/useTituloPagina';
import { idadePorExtenso, nomeParentesco, NOME_SEXO, NOME_SITUACAO } from '../../dominio/pacientes';
import { useSessao } from '../../sessao/contextoSessao';
import type { EstadoPacienteCriado } from '../NovoPaciente';
import { SecaoColeta } from './SecaoColeta';
import { SecaoConsentimentos } from './SecaoConsentimentos';
import { SecaoDiagnostico } from './SecaoDiagnostico';
import { SecaoSessoes } from './SecaoSessoes';
import estilos from './Paciente.module.css';

/** UC04/UC05/UC13 — o paciente: dados, coleta, sessões, situação diagnóstica, consentimentos e responsável. */
export function Paciente() {
  const { id = '' } = useParams();
  const { api } = useSessao();
  const local = useLocation();
  const recemCriado = (local.state as EstadoPacienteCriado | null)?.criado === true;
  const [registrando, setRegistrando] = useState(false);

  const detalhe = useQuery({ queryKey: chavesPacientes.detalhe(id), queryFn: () => obterPaciente(api, id) });
  useTituloPagina(detalhe.data?.paciente.nome ?? 'Paciente');

  if (detalhe.isPending) {
    return <Carregando texto="Carregando paciente…" />;
  }

  if (detalhe.isError) {
    if (detalhe.error instanceof ErroApi && detalhe.error.status === 404) {
      return (
        <section className={estilos.pagina} aria-labelledby="titulo-paciente">
          <h1 id="titulo-paciente">Paciente não encontrado</h1>
          <p>Ele não existe ou está sob responsabilidade de outro médico.</p>
          <p>
            <Link to="/pacientes">Voltar para Pacientes</Link>
          </p>
        </section>
      );
    }

    return <FalhaAoCarregar erro={detalhe.error} tentar={() => void detalhe.refetch()} />;
  }

  const { paciente, responsavel, diagnostico, consentimentos, sessoes } = detalhe.data;

  return (
    <section className={estilos.pagina} aria-labelledby="titulo-paciente">
      <header className={estilos.cabecalho}>
        <p className={estilos.voltar}>
          <Link to="/pacientes">Pacientes</Link>
        </p>
        <h1 id="titulo-paciente">{paciente.nome ?? 'Dados eliminados a pedido do titular'}</h1>
        <dl className={estilos.resumo}>
          <div>
            <dt>Idade</dt>
            <dd>{paciente.anonimizado ? '—' : idadePorExtenso(paciente.dataNascimento)}</dd>
          </div>
          <div>
            <dt>Nascimento</dt>
            <dd>{data(paciente.dataNascimento)}</dd>
          </div>
          <div>
            <dt>Sexo biológico</dt>
            <dd>{NOME_SEXO[paciente.sexo]}</dd>
          </div>
          <div>
            <dt>Situação</dt>
            <dd>{NOME_SITUACAO[paciente.situacao]}</dd>
          </div>
        </dl>
      </header>

      {recemCriado && consentimentos.length === 0 && (
        <Mensagem>Paciente cadastrado. Registre o TCLE assinado para liberar a coleta.</Mensagem>
      )}

      <SecaoColeta
        pacienteId={paciente.id}
        podeColetar={paciente.podeColetar}
        anonimizado={paciente.anonimizado}
        sessoes={sessoes}
        registrarConsentimento={() => {
          setRegistrando(true);
        }}
      />

      <SecaoSessoes sessoes={sessoes} />

      {!paciente.anonimizado && (
        <SecaoDiagnostico
          pacienteId={paciente.id}
          situacao={paciente.situacao}
          diagnostico={diagnostico}
          dataNascimento={paciente.dataNascimento}
        />
      )}

      {!paciente.anonimizado && (
        <SecaoConsentimentos
          pacienteId={paciente.id}
          consentimentos={consentimentos}
          registrando={registrando}
          setRegistrando={setRegistrando}
        />
      )}

      {!paciente.anonimizado && (
        <section className={estilos.secao} aria-labelledby="titulo-responsavel">
          <h2 id="titulo-responsavel">Responsável legal</h2>
          {responsavel ? (
            <dl className={estilos.dados}>
              <div>
                <dt>Nome</dt>
                <dd>{responsavel.nome}</dd>
              </div>
              <div>
                <dt>Parentesco</dt>
                <dd>{nomeParentesco(responsavel.parentesco)}</dd>
              </div>
              <div>
                <dt>Contato</dt>
                <dd>{responsavel.contato ?? '—'}</dd>
              </div>
            </dl>
          ) : (
            <p className={estilos.nota}>Não informado. O paciente era maior de idade no cadastro.</p>
          )}
        </section>
      )}
    </section>
  );
}
