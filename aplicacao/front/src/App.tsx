import { Route, Routes } from 'react-router';
import { Layout } from './componentes/Layout';
import { CadastrosPendentes } from './paginas/CadastrosPendentes';
import { EmConstrucao } from './paginas/EmConstrucao';
import { Entrar } from './paginas/Entrar';
import { Inicio } from './paginas/Inicio';
import { NaoEncontrada } from './paginas/NaoEncontrada';
import { SolicitarCadastro } from './paginas/SolicitarCadastro';
import { RotaProtegida } from './sessao/RotaProtegida';

/** Rotas do doc 09, seção 3. Telas dos marcos F3–F5 ainda são reservas. */
export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/entrar" element={<Entrar />} />
        <Route path="/cadastro" element={<SolicitarCadastro />} />

        <Route element={<RotaProtegida />}>
          <Route index element={<Inicio />} />
        </Route>

        <Route element={<RotaProtegida papel="ADMIN" />}>
          <Route path="/admin/solicitacoes" element={<CadastrosPendentes />} />
        </Route>

        <Route element={<RotaProtegida papel="MEDICO" />}>
          <Route
            path="/pacientes"
            element={
              <EmConstrucao
                titulo="Pacientes"
                marco="F3"
                descricao="Aqui ficarão a lista e a busca de pacientes, o cadastro e o registro dos consentimentos."
              />
            }
          />
          <Route
            path="/pacientes/*"
            element={
              <EmConstrucao
                titulo="Paciente"
                marco="F3"
                descricao="Dados do paciente, consentimentos e sessões de coleta."
              />
            }
          />
          <Route
            path="/sessoes/:id"
            element={
              <EmConstrucao titulo="Coleta" marco="F4" descricao="Coleta das dez impressões digitais na ficha decadactilar." />
            }
          />
          <Route
            path="/analises/:id"
            element={
              <EmConstrucao
                titulo="Resultado da análise"
                marco="F5"
                descricao="Indicação complementar de apoio à decisão do médico, com o aviso de pesquisa e os indicadores."
              />
            }
          />
        </Route>

        <Route path="*" element={<NaoEncontrada />} />
      </Route>
    </Routes>
  );
}
