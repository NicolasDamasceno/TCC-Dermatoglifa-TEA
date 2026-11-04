import { Route, Routes } from 'react-router';
import { Layout } from './componentes/Layout';
import { CadastrosPendentes } from './paginas/CadastrosPendentes';
import { Coleta } from './paginas/coleta/Coleta';
import { EmConstrucao } from './paginas/EmConstrucao';
import { Entrar } from './paginas/Entrar';
import { Inicio } from './paginas/Inicio';
import { NaoEncontrada } from './paginas/NaoEncontrada';
import { NovoPaciente } from './paginas/NovoPaciente';
import { Paciente } from './paginas/paciente/Paciente';
import { Pacientes } from './paginas/Pacientes';
import { SolicitarCadastro } from './paginas/SolicitarCadastro';
import { RotaProtegida } from './sessao/RotaProtegida';

/** Rotas do doc 09, seção 3. A tela do marco F5 ainda é reserva. */
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
          <Route path="/pacientes" element={<Pacientes />} />
          <Route path="/pacientes/novo" element={<NovoPaciente />} />
          <Route path="/pacientes/:id" element={<Paciente />} />
          <Route path="/sessoes/:id" element={<Coleta />} />
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
