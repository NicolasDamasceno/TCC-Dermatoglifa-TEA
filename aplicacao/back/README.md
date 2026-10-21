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

# Banco local (exemplo com psql)
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

# Cria as tabelas e sobe a API
dotnet ef database update -p src/Dermatoglifia.Infraestrutura -s src/Dermatoglifia.Api
dotnet run --project src/Dermatoglifia.Api
```

A API não inicia sem a connection string ou sem uma chave JWT de pelo menos 32
bytes, nem com migrations pendentes — falha rápida de propósito, com a instrução
na mensagem. O modelo de variáveis está em `.env.example`.

## Rotas disponíveis

| Rota | Quem | O que faz |
|---|---|---|
| `POST /api/auth/login` | público | entra e recebe o token |
| `POST /api/medicos/solicitacoes` | público | médico pede cadastro (fica pendente) |
| `GET /api/medicos/solicitacoes` | admin | lista cadastros pendentes |
| `POST /api/medicos/{id}/aprovacao` | admin | aprova, informando o vínculo com o protocolo |
| `POST /api/medicos/{id}/recusa` | admin | recusa, informando o motivo |

## Testes

```sh
dotnet test
```

Os testes de integração sobem a API contra um PostgreSQL real, num banco criado e
apagado a cada execução. Por padrão usam um contêiner (Testcontainers, exige
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
| M3 — pacientes, identificação separada, consentimento e auditoria | próximo |
| M4 a M6 | pendentes |
