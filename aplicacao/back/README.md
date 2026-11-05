# aplicacao/back/

API em **.NET 10 (LTS)** com ASP.NET Core, EF Core e PostgreSQL. O desenho
completo está em [`documentacao/08-back-end.md`](../../documentacao/08-back-end.md);
antes de mexer no código, ler também o `CLAUDE.md` da raiz.

```
Dermatoglifia.sln
src/
  Dermatoglifia.Dominio/          entidades e regras (sem dependências)
  Dermatoglifia.Aplicacao/        casos de uso
  Dermatoglifia.Infraestrutura/   EF Core + PostgreSQL, Identity, migrations
  Dermatoglifia.Api/              controllers e configuração
tests/
  Dermatoglifia.Dominio.Testes/   testes das regras do domínio
  Dermatoglifia.Api.Testes/       testes de integração (API + PostgreSQL)
```

## Pré-requisitos

- .NET SDK 10
- PostgreSQL 16 ou superior

## Primeira execução

```sh
cd aplicacao/back
dotnet tool restore                       # instala o dotnet-ef da versão do projeto

# Banco local: pelo Docker, na raiz do repositório (cp .env.example .env;
# docker compose up -d banco), ou com um PostgreSQL instalado:
createuser -P dermatoglifia               # defina uma senha
createdb -O dermatoglifia dermatoglifia

# Connection string fora do Git, nos segredos do usuário
dotnet user-secrets set "ConnectionStrings:Banco" \
  "Host=localhost;Port=5432;Database=dermatoglifia;Username=dermatoglifia;Password=<sua senha>" \
  --project src/Dermatoglifia.Api

# Chave do JWT e primeiro administrador, também nos segredos do usuário
dotnet user-secrets set "Jwt:Chave" "$(openssl rand -base64 32)" --project src/Dermatoglifia.Api
dotnet user-secrets set "AdminInicial:Email" "<seu e-mail>" --project src/Dermatoglifia.Api
dotnet user-secrets set "AdminInicial:Senha" "<senha com 10+ caracteres, letra e número>" --project src/Dermatoglifia.Api

# Pasta das imagens cifradas (FORA do repositório) e chave de cifragem
dotnet user-secrets set "Armazenamento:Diretorio" "$HOME/dermatoglifia-imagens" --project src/Dermatoglifia.Api
dotnet user-secrets set "Armazenamento:Chaves:1" "$(openssl rand -base64 32)" --project src/Dermatoglifia.Api

# Cria as tabelas e sobe a API
dotnet ef database update -p src/Dermatoglifia.Infraestrutura -s src/Dermatoglifia.Api
dotnet run --project src/Dermatoglifia.Api
```

A API não inicia sem a connection string, sem uma chave JWT de pelo menos 32
bytes, sem a pasta e a chave de armazenamento, sem um endereço válido do Serviço de
IA (`Inferencia:UrlBase`, padrão `http://localhost:8000`), nem com migrations pendentes — falha rápida de propósito, com a instrução
na mensagem. O modelo de variáveis está em `.env.example`.

## Rotas disponíveis

| Rota | Quem | O que faz |
|---|---|---|
| `POST /api/auth/login` | público | entra e recebe o token |
| `POST /api/medicos/solicitacoes` | público | médico pede cadastro (fica pendente) |
| `GET /api/medicos/solicitacoes` | admin | lista cadastros pendentes |
| `POST /api/medicos/{id}/aprovacao` | admin | aprova, informando o vínculo com o protocolo |
| `POST /api/medicos/{id}/recusa` | admin | recusa, informando o motivo |
| `GET /api/pacientes?busca=` | médico | lista os próprios pacientes |
| `POST /api/pacientes` | médico | cadastra paciente (identificação guardada à parte) |
| `GET /api/pacientes/{id}` | médico | detalhe, responsável e consentimentos |
| `POST /api/pacientes/{id}/consentimentos` | médico | registra TCLE ou TALE |
| `POST /api/pacientes/{id}/consentimentos/{cid}/revogacao` | médico | revoga um consentimento |
| `POST /api/pacientes/{id}/sessoes` | médico | inicia a coleta das 10 digitais |
| `GET /api/sessoes/{id}` | médico | situação de cada dedo |
| `PUT /api/sessoes/{id}/digitais/{mao}/{dedo}` | médico | envia ou refaz a imagem de um dedo (PNG, BMP ou TIFF, ≥ 500 ppi, até 5 MB) |
| `POST /api/sessoes/{id}/digitais/{mao}/{dedo}/nao-coletavel` | médico | registra dedo que não pode ser coletado |
| `POST /api/sessoes/{id}/conclusao` | médico | conclui a sessão; se completa, já pede e devolve a análise |
| `POST /api/sessoes/{id}/cancelamento` | médico | cancela a sessão |
| `GET /api/analises/{id}` | médico | resultado com aviso, alertas, padrões, indicadores e modelo |
| `POST /api/analises/{id}/nova-tentativa` | médico | reprocessa uma análise com erro |
| `PATCH /api/analises/{id}/observacao` | médico | registra observação sobre o resultado |
| `POST /api/analises/{id}/registro-prontuario` | médico | confirma o registro do uso de IA no prontuário |

**Serviço de IA.** A análise chama o serviço Python do ADR-0001 em
`Inferencia:UrlBase`. Enquanto ele não existir, sessões concluídas geram análises
com status `ERRO` ("Serviço de IA indisponível"), que podem ser reprocessadas
depois. A versão de modelo que o serviço informa precisa estar cadastrada e ativa
na tabela `modelo_ia`; o cadastro pela API (UC10) ainda não existe — por ora é feito
direto no banco, conforme `modelo_producao/README.md`.

Paciente de outro médico responde `404`, como se não existisse. Todo erro vem em
`application/problem+json` com título em português e `traceId`; sem login, até
rota inexistente responde `401`.

**Front-end local.** Em desenvolvimento, o CORS já libera `http://localhost:5173`
(porta padrão do Vite); outras origens vão em `Cors:OrigensPermitidas`.

## Testes

```sh
dotnet test
```

Os testes de integração sobem a API contra um PostgreSQL real, num banco criado e
apagado a cada execução, com uma IA falsa (`tests/.../IaFalsa.cs`) no lugar do
serviço Python: nenhuma chamada sai da máquina. Por padrão usam um contêiner (Testcontainers, exige
Docker). Sem Docker, aponte para um PostgreSQL existente:

```sh
TESTES_BANCO="Host=localhost;Port=5432;Username=postgres;Password=<senha>" dotnet test
```

Todos os testes usam **dados sintéticos**; nenhuma digital ou dado real entra em
`tests/` (RNF08).

## Nova migration

```sh
dotnet ef migrations add <NomeEmPascalCase> \
  -p src/Dermatoglifia.Infraestrutura -s src/Dermatoglifia.Api -o Persistencia/Migracoes
```

Revise o SQL gerado (`dotnet ef migrations script`) antes de commitar e atualize
o doc 05 se a estrutura do banco mudar.

## Andamento (doc 08, seção 11)

| Marco | Situação |
|---|---|
| M1 — solução, domínio com testes, `AppDbContext` e migration inicial | concluído |
| M2 — Identity + JWT, solicitação e aprovação de médico, admin inicial | concluído |
| M3 — pacientes, identificação separada, consentimento e auditoria | concluído |
| M4 — sessão de coleta, upload, normalização e cifragem das digitais | concluído |
| M5 — cliente do Serviço de IA, verificação de consistência e análise | concluído |
| M6 — ProblemDetails completo, CORS, cabeçalhos de segurança, testes transversais | concluído |
| M7 — situação diagnóstica (UC13; ADR-0009) | concluído |

Com o M6, os fluxos do MVP do back-end (UC01–UC08) estão implementados; o M7
acrescenta a UC13. Ficam para depois: cadastro de modelo pela API (UC10),
histórico do paciente (UC09) e exportação para treino (UC14).
