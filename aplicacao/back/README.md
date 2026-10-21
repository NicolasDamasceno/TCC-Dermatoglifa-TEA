# aplicacao/back/

API em **.NET 10 (LTS)** com ASP.NET Core, EF Core e PostgreSQL. O desenho
completo está em [`documentacao/08-back-end.md`](../../documentacao/08-back-end.md);
antes de mexer no código, ler também o `CLAUDE.md` da raiz.

```
Dermatoglifia.sln
src/
  Dermatoglifia.Dominio/          entidades e regras (sem dependências)
  Dermatoglifia.Aplicacao/        casos de uso (a partir do M2)
  Dermatoglifia.Infraestrutura/   EF Core + PostgreSQL, Identity, migrations
  Dermatoglifia.Api/              controllers e configuração
tests/
  Dermatoglifia.Dominio.Testes/   testes das regras do domínio
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

# Cria as tabelas
dotnet ef database update -p src/Dermatoglifia.Infraestrutura -s src/Dermatoglifia.Api
```

A API não inicia sem a connection string — falha rápida de propósito, para não
rodar com configuração incompleta. O modelo de variáveis está em `.env.example`.

## Testes

```sh
dotnet test
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
| M2 — Identity + JWT, solicitação e aprovação de médico | próximo |
| M3 a M6 | pendentes |
