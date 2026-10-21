# 04 — Diagramas UML

Diagramas em Mermaid (renderizam no GitHub). Nomes do domínio em português,
como definido no `CLAUDE.md`.

## 1. Diagrama de classes do domínio (back-end .NET)

```mermaid
classDiagram
    direction LR

    class Usuario {
        +Guid Id
        +string Nome
        +string Email
        -string SenhaHash
        +PapelUsuario Papel
        +StatusCadastro Status
        +RegistrarFalhaLogin()
        +EstaBloqueado() bool
    }

    class Medico {
        +string Crm
        +string UfCrm
        +string Especialidade
        +string VinculoProtocolo
        +Aprovar(Usuario admin)
        +Recusar(Usuario admin, string motivo)
    }

    class Paciente {
        +Guid Id
        +SexoBiologico Sexo
        +SituacaoDiagnostica Situacao
        +string FonteDiagnostico
        +PodeColetar() bool
        +AtualizarSituacao(SituacaoDiagnostica, string fonte)
        +Anonimizar()
    }

    class IdentificacaoPaciente {
        <<dados identificáveis>>
        +string Nome
        +DateOnly DataNascimento
        +string ResponsavelNome
        +string ResponsavelParentesco
        +string ResponsavelContato
        +IdadeEmMeses(DateOnly data) int
        +EhMenorDeIdade() bool
    }

    class Consentimento {
        +Guid Id
        +TipoTermo Tipo
        +string VersaoTermo
        +bool AutorizaAnalise
        +bool CienteUsoIA
        +bool AutorizaUsoTreino
        +DateOnly AssinadoEm
        +DateTime? RevogadoEm
        +EstaVigente() bool
        +Revogar()
    }

    class SessaoColeta {
        +Guid Id
        +MetodoCaptura Metodo
        +TipoImpressao TipoImpressao
        +int IdadeMeses
        +StatusSessao Status
        +DateTime IniciadaEm
        +DateTime? ConcluidaEm
        +RegistrarDigital(Mao, Dedo, ArquivoImagem)
        +MarcarNaoColetavel(Mao, Dedo, string motivo)
        +Concluir()
        +Cancelar()
        +EstaCompleta() bool
    }

    class Digital {
        +Guid Id
        +Mao Mao
        +Dedo Dedo
        +bool Coletavel
        +string CaminhoArquivo
        +string HashSha256
        +int ResolucaoDpi
    }

    class Analise {
        +Guid Id
        +StatusAnalise Status
        +IndicadoresDermatoglificos Indicadores
        +decimal? EscoreRisco
        +string ObservacaoMedico
        +string VersaoAviso
        +DateTime? RegistradoProntuarioEm
        +IniciarProcessamento()
        +Concluir(RespostaInferencia)
        +Falhar(string mensagem)
        +ConfirmarRegistroProntuario()
    }

    class ResultadoDigital {
        +string ClasseImagem
        +PadraoDigital PadraoPrevisto
        +decimal Confianca
        +bool AlertaQualidade
    }

    class IndicadoresDermatoglificos {
        <<value object>>
        +int QtdVerticilos
        +int QtdAlcasUlnares
        +int QtdAlcasRadiais
        +int QtdArcos
        +int QtdArcosTendidos
        +int DiscordanciaBilateral
        +int DiscordanciaBilateralComPolegar
    }

    class ModeloIA {
        +Guid Id
        +string Versao
        +string HashSha256
        +MetricasTeste Metricas
        +bool Ativo
    }

    class PadraoDigital {
        <<enumeration>>
        ARCO
        ARCO_TENDIDO
        ALCA_ULNAR
        ALCA_RADIAL
        VERTICILO
    }

    Usuario <|-- Medico
    Medico "1" --> "*" Paciente : responsável
    Paciente "1" -- "1" IdentificacaoPaciente : pseudonimização
    Paciente "1" *-- "*" Consentimento
    Paciente "1" --> "*" SessaoColeta
    Medico "1" --> "*" SessaoColeta : conduz
    SessaoColeta "1" *-- "0..10" Digital
    SessaoColeta "1" --> "0..1" Analise
    Analise "*" --> "1" ModeloIA : produzida por
    Analise "1" *-- "10" ResultadoDigital
    Analise *-- IndicadoresDermatoglificos
    ResultadoDigital --> Digital
    ResultadoDigital --> PadraoDigital
```

> No banco, `Medico` é uma tabela 1-para-1 com `usuario` (ver doc 05); no código
> a herança é a forma mais direta de expressar que todo médico é um usuário.
> `IdentificacaoPaciente` fica separada de `Paciente` para que biometria e
> resultados só conheçam o identificador pseudônimo (LGPD art. 13, §4º).

## 2. Classes de serviço (camadas do back-end)

```mermaid
classDiagram
    direction TB

    class PacientesController
    class ColetasController
    class AnalisesController

    class IServicoPacientes {
        <<interface>>
        +Cadastrar(CadastroPacienteDto) PacienteDto
        +RegistrarConsentimento(Guid pacienteId, ConsentimentoDto)
    }
    class IServicoColeta {
        <<interface>>
        +IniciarSessao(Guid pacienteId) SessaoDto
        +EnviarDigital(Guid sessaoId, Mao, Dedo, IFormFile)
        +Concluir(Guid sessaoId) AnaliseDto
    }
    class IServicoAnalise {
        <<interface>>
        +Solicitar(Guid sessaoId) AnaliseDto
        +Obter(Guid analiseId) AnaliseDto
    }
    class IClienteInferencia {
        <<interface>>
        +Analisar(RequisicaoInferencia) RespostaInferencia
    }
    class IArmazenamentoImagens {
        <<interface>>
        +Salvar(Stream, string caminho) string hash
        +Abrir(string caminho) Stream
        +Apagar(string caminho)
    }
    class IAuditoria {
        <<interface>>
        +Registrar(string acao, string entidade, Guid id)
    }

    class ClienteInferenciaHttp
    class ArmazenamentoLocalCifrado
    class AppDbContext

    PacientesController --> IServicoPacientes
    ColetasController --> IServicoColeta
    AnalisesController --> IServicoAnalise
    IServicoColeta --> IArmazenamentoImagens
    IServicoColeta --> IServicoAnalise
    IServicoAnalise --> IClienteInferencia
    IServicoAnalise --> IArmazenamentoImagens
    IServicoPacientes ..> IAuditoria
    IServicoColeta ..> IAuditoria
    IServicoAnalise ..> IAuditoria
    IClienteInferencia <|.. ClienteInferenciaHttp
    IArmazenamentoImagens <|.. ArmazenamentoLocalCifrado
    IServicoPacientes ..> AppDbContext
    IServicoColeta ..> AppDbContext
    IServicoAnalise ..> AppDbContext
```

As interfaces isolam o que muda: o cliente de inferência pode trocar de HTTP para
ONNX dentro do .NET (ADR-0001) e o armazenamento pode trocar de disco local para
outro destino sem afetar os serviços.

## 3. Sequência — autenticação (UC01)

```mermaid
sequenceDiagram
    autonumber
    actor M as Médico
    participant F as Front (React)
    participant B as API (.NET)
    participant DB as PostgreSQL

    M->>F: e-mail e senha
    F->>B: POST /api/auth/login
    B->>DB: busca usuário por e-mail
    DB-->>B: usuário (hash, status, bloqueio)
    alt credenciais válidas e status ATIVO
        B->>DB: zera falhas, grava último acesso, log LOGIN_OK
        B-->>F: 200 { token, papel, expiraEm }
        F-->>M: tela inicial
    else inválidas, pendente, ou bloqueado
        B->>DB: incrementa falhas, log LOGIN_FALHA
        B-->>F: 401 / 403 com mensagem genérica
        F-->>M: aviso
    end
```

## 4. Sequência — cadastro do paciente e consentimento (UC04 + UC05)

```mermaid
sequenceDiagram
    autonumber
    actor M as Médico
    participant F as Front
    participant B as API
    participant DB as PostgreSQL

    M->>F: dados do paciente e do responsável
    F->>B: POST /api/pacientes
    B->>B: valida (menor de idade exige responsável)
    B->>DB: INSERT paciente (pseudônimo) + INSERT identificacao_paciente
    B->>DB: log PACIENTE_CRIADO
    B-->>F: 201 { pacienteId }
    M->>F: termo, versão, data, autoriza análise, ciente do uso de IA, autoriza uso em treino
    F->>B: POST /api/pacientes/{id}/consentimentos
    B->>DB: INSERT consentimento
    B-->>F: 201
    F-->>M: coleta liberada se autoriza análise e ciente do uso de IA
```

## 5. Sequência — coleta das 10 digitais e análise (UC06 + UC07 + UC08)

```mermaid
sequenceDiagram
    autonumber
    actor M as Médico
    participant F as Front
    participant B as API (.NET)
    participant A as Armazenamento cifrado
    participant DB as PostgreSQL
    participant IA as Serviço de IA (Python)

    M->>F: iniciar coleta
    F->>B: POST /api/pacientes/{id}/sessoes
    B->>DB: verifica TCLE vigente (autoriza análise + ciente do uso de IA)
    B->>DB: INSERT sessao_coleta (EM_ANDAMENTO, método, tipo de impressão, idade em meses)
    B-->>F: 201 { sessaoId }

    loop para cada um dos 10 dedos
        M->>F: seleciona imagem do dedo
        F->>B: PUT /api/sessoes/{id}/digitais/{mao}/{dedo} (multipart)
        B->>B: valida formato, resolução, tamanho
        B->>A: grava arquivo cifrado
        A-->>B: caminho + SHA-256
        B->>DB: UPSERT digital
        B-->>F: 200 (dedo coletado)
    end

    M->>F: concluir coleta
    F->>B: POST /api/sessoes/{id}/conclusao
    B->>DB: sessão COMPLETA, INSERT analise (PENDENTE)
    B->>A: lê e decifra as 10 imagens
    B->>IA: POST /inferencia (10 imagens + mão/dedo + sexo + idade, sem identificação)
    alt sucesso
        IA-->>B: padrão e probabilidades por dedo, indicadores, escore, versão do modelo
        B->>DB: INSERT resultado_digital x10, UPDATE analise (CONCLUIDA)
        B-->>F: 200 { analiseId, status: CONCLUIDA }
    else falha ou timeout
        B->>DB: UPDATE analise (ERRO, mensagem)
        B-->>F: 200 { analiseId, status: ERRO }
    end

    M->>F: abrir resultado
    F->>B: GET /api/analises/{id}
    B->>DB: busca análise (verifica vínculo médico-paciente) + log ANALISE_VISUALIZADA
    B-->>F: resultado + versão e métricas do modelo
    F-->>M: resultado com aviso de protótipo de pesquisa
    M->>F: confirma registro do uso de IA no prontuário
    F->>B: POST /api/analises/{id}/registro-prontuario
    B->>DB: UPDATE analise (registrado_prontuario_em)
```

A chamada ao Serviço de IA é **síncrona** no MVP: são 10 imagens pequenas e a
inferência de uma CNN desse porte cabe em poucos segundos mesmo em CPU. Se o
tempo crescer, a análise passa para uma fila em segundo plano sem mudar o
contrato com o front (o status `PENDENTE/PROCESSANDO` já existe para isso).

## 6. Estados — sessão de coleta

```mermaid
stateDiagram-v2
    [*] --> EM_ANDAMENTO : iniciar
    EM_ANDAMENTO --> EM_ANDAMENTO : enviar/refazer digital
    EM_ANDAMENTO --> COMPLETA : concluir com 10 digitais
    EM_ANDAMENTO --> INCOMPLETA : concluir com dedo não coletável
    EM_ANDAMENTO --> CANCELADA : cancelar
    COMPLETA --> [*]
    INCOMPLETA --> [*]
    CANCELADA --> [*]
```

## 7. Estados — análise

```mermaid
stateDiagram-v2
    [*] --> PENDENTE : sessão COMPLETA
    PENDENTE --> PROCESSANDO : enviada ao Serviço de IA
    PROCESSANDO --> CONCLUIDA : resposta válida
    PROCESSANDO --> ERRO : falha ou timeout
    ERRO --> PROCESSANDO : nova tentativa
    CONCLUIDA --> [*]
```
