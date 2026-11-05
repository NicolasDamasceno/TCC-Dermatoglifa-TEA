import { Route, Routes } from 'react-router';
import { Layout } from './componentes/Layout';
import { CadastrosPendentes } from './paginas/CadastrosPendentes';
import { Coleta } from './paginas/coleta/Coleta';
import { Entrar } from './paginas/Entrar';
import { Inicio } from './paginas/Inicio';
import { Modelos } from './paginas/Modelos';
import { NaoEncontrada } from './paginas/NaoEncontrada';
import { NovoPaciente } from './paginas/NovoPaciente';
import { Resultado } from './paginas/resultado/Resultado';
import { Paciente } from './paginas/paciente/Paciente';
import { Pacientes } from './paginas/Pacientes';
import { SolicitarCadastro } from './paginas/SolicitarCadastro';
import { RotaProtegida } from './sessao/RotaProtegida';

/** Rotas do doc 09, seção 3. */
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
          <Route path="/admin/modelos" element={<Modelos />} />
        </Route>

        <Route element={<RotaProtegida papel="MEDICO" />}>
          <Route path="/pacientes" element={<Pacientes />} />
          <Route path="/pacientes/novo" element={<NovoPaciente />} />
          <Route path="/pacientes/:id" element={<Paciente />} />
          <Route path="/sessoes/:id" element={<Coleta />} />
          <Route path="/analises/:id" element={<Resultado />} />
        </Route>

        <Route path="*" element={<NaoEncontrada />} />
      </Route>
    </Routes>
  );
}
