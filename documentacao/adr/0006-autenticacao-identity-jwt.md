# ADR-0006: Autenticação com ASP.NET Core Identity (Core) e token JWT

**Status:** Proposto
**Data:** 2026-10-20
**Decisores:** Nicolas Damasceno (autor), orientador do TCC

## Contexto

Os casos de uso UC01–UC03 exigem login de médicos e administradores, cadastro
pendente até aprovação, bloqueio após 5 falhas (RNF05) e hash adaptativo de senha.
O front é uma SPA React separada da API. Os dados protegidos são sensíveis
(biometria e saúde), então a parte de segurança não deve ser reinventada.

## Decisão

Usar **ASP.NET Core Identity no modo Core** (`AddIdentityCore<Usuario>`) com
armazenamento no EF Core, apenas para o que ele faz bem — usuários, hash de senha,
política de senha e bloqueio — e emitir **token JWT** próprio no endpoint
`POST /api/auth/login`, validado pelo `JwtBearer` em todas as rotas.

- `Usuario : IdentityUser<Guid>` com os campos do domínio (`Nome`, `Papel`, `Status`).
- Tabelas do Identity mapeadas para nomes em português/snake_case (`usuario` etc.);
  não usamos as tabelas de papéis do Identity: o papel é um campo de `usuario`
  e vira *claim* no token.
- Bloqueio: 5 falhas → 15 minutos (`LockoutOptions`).
- Senha: mínimo de 10 caracteres, exigindo letras e números.
- Token de acesso com validade de 60 minutos, assinado com HMAC-SHA256; chave em
  `dotnet user-secrets` (desenvolvimento) ou variável de ambiente (servidor).
- Sem *refresh token* no MVP: expirado o token, o médico faz login de novo.
- O login só emite token para `Status = ATIVO`; o token carrega `sub`, `papel` e
  um *security stamp* para que inativar um usuário invalide tokens antigos na
  próxima requisição.
- Políticas de autorização: `ApenasMedico` e `ApenasAdmin`.

## Opções consideradas

### Opção A: Identity Core + JWT próprio (escolhida)
| Dimensão | Avaliação |
|---|---|
| Complexidade | Média |
| Segurança | Hash, bloqueio e *security stamp* já testados pela plataforma |
| Controle | Total sobre o formato do token e as claims |
| Familiaridade | Padrão mais documentado em material de ASP.NET Core |

### Opção B: endpoints prontos do Identity (`MapIdentityApi`)
**Prós:** quase nada a escrever.
**Contras:** os tokens emitidos não são JWT padrão e os endpoints (registro aberto,
recuperação de senha) não seguem o fluxo de aprovação do UC02/UC03; customizar
acaba dando mais trabalho que a opção A.

### Opção C: autenticação toda escrita à mão (tabela própria + `PasswordHasher`)
**Prós:** modelo de dados mínimo.
**Contras:** bloqueio, invalidação de sessão e comparação segura ficam por conta do
projeto — exatamente o tipo de código que não deve ser reinventado num sistema
com dados sensíveis.

### Opção D: provedor externo (Keycloak, Entra ID)
**Prós:** MFA e gestão de usuários prontos.
**Contras:** mais um servidor para manter; desproporcional para um TCC.

## Consequências

- A tabela `usuario` do doc 05 passa a ter as colunas do Identity
  (`email_normalizado`, `security_stamp`, `concurrency_stamp`,
  `access_failed_count`, `lockout_end`, `lockout_enabled`). `tentativas_falhas`
  e `bloqueado_ate` deixam de existir — o Identity cumpre esse papel.
- Mais fácil: atender RNF05 sem código próprio de bloqueio; adicionar MFA (RNF12)
  depois com os recursos do Identity.
- Mais difícil: mapear as tabelas do Identity para o padrão de nomes do banco.
- Revisitar: *refresh token* em cookie `HttpOnly` se o tempo de sessão incomodar
  os médicos.

## Ações

1. [ ] Configurar `AddIdentityCore<Usuario>` + `AddEntityFrameworkStores`.
2. [ ] Implementar `ServicoToken` e o endpoint de login.
3. [ ] Criar o primeiro ADMIN por comando de inicialização com senha vinda de variável de ambiente (nunca no código).
