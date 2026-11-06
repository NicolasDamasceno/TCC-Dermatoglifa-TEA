import { Link, NavLink, Outlet, useLocation } from 'react-router';
import { useSessao } from '../sessao/contextoSessao';
import { AvisoPesquisa } from './AvisoPesquisa';
import estilos from './Layout.module.css';

/**
 * Moldura de todas as telas: barra (marca, navegação do papel, usuário, Sair),
 * faixa de pesquisa fixa e conteúdo alinhado à esquerda (doc 09, seção 2).
 */
export function Layout() {
  const { sessao, sair } = useSessao();
  // Coleta e resultado pertencem ao trabalho com o paciente: "Pacientes" segue marcado.
  const naAreaDePacientes = /^\/(pacientes|sessoes|analises)(\/|$)/.test(useLocation().pathname);

  return (
    <>
      <a href="#conteudo" className={estilos.pular}>
        Pular para o conteúdo
      </a>
      <header className={estilos.barra}>
        <div className={estilos.barraInterna}>
          <Link to="/" className={estilos.marca}>
            Dermatoglifia TEA
          </Link>

          {sessao && (
            <>
              <nav aria-label="Principal" className={estilos.navegacao}>
                {sessao.usuario.papel === 'MEDICO' ? (
                  <NavLink to="/pacientes" className={() => classeLink({ isActive: naAreaDePacientes })}>
                    Pacientes
                  </NavLink>
                ) : (
                  <>
                    <NavLink to="/pacientes" className={() => classeLink({ isActive: naAreaDePacientes })}>
                      Pacientes
                    </NavLink>
                    <NavLink to="/admin/solicitacoes" className={classeLink}>
                      Cadastros pendentes
                    </NavLink>
                    <NavLink to="/admin/modelos" className={classeLink}>
                      Modelos de IA
                    </NavLink>
                    <NavLink to="/admin/auditoria" className={classeLink}>
                      Auditoria
                    </NavLink>
                  </>
                )}
              </nav>
              <div className={estilos.usuario}>
                <span>{sessao.usuario.nome}</span>
                <button
                  type="button"
                  className={estilos.sair}
                  onClick={() => {
                    sair('saiu');
                  }}
                >
                  Sair
                </button>
              </div>
            </>
          )}
        </div>
      </header>
      <AvisoPesquisa />
      <main id="conteudo" tabIndex={-1} className={estilos.conteudo}>
        <Outlet />
      </main>
    </>
  );
}

function classeLink({ isActive }: { isActive: boolean }): string {
  return [estilos.link, isActive ? estilos.ativo : undefined].filter(Boolean).join(' ');
}
