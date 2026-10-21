# ADR-0003: PostgreSQL como banco de dados

**Status:** Proposto
**Data:** 2026-10-20
**Decisores:** Nicolas Damasceno (autor), orientador do TCC

## Contexto

O back-end é .NET com Entity Framework Core. O volume é pequeno (dezenas a
centenas de pacientes num TCC), mas os dados são sensíveis e precisam de
integridade referencial, restrições declarativas (unicidade por dedo, um único
modelo ativo) e algum dado semiestruturado (probabilidades e métricas do modelo).
O projeto não tem orçamento para licenças.

## Decisão

Usar **PostgreSQL** com o provedor Npgsql do EF Core, rodando localmente (ou em
contêiner) no desenvolvimento.

## Opções consideradas

### Opção A: PostgreSQL
| Dimensão | Avaliação |
|---|---|
| Complexidade | Baixa |
| Custo | Gratuito, código aberto |
| Recursos | `jsonb`, índices parciais únicos, `inet`, `CHECK`, tipos enumerados |
| Familiaridade | Média; SQL padrão |

**Prós:** índices parciais resolvem "um modelo ativo" e "uma sessão em andamento"
sem lógica extra; `jsonb` para probabilidades/métricas; roda em qualquer SO.
**Contras:** exige instalar o servidor (ou Docker).

### Opção B: SQL Server (Express/Developer)
| Dimensão | Avaliação |
|---|---|
| Complexidade | Baixa |
| Custo | Express gratuito com limites; Developer não pode ir para produção |
| Recursos | Índices filtrados, JSON em `nvarchar` |
| Familiaridade | Alta no ecossistema .NET |

**Prós:** integração tradicional com .NET.
**Contras:** licenciamento se o projeto sair do ambiente acadêmico; JSON menos natural.

### Opção C: SQLite
**Prós:** zero instalação.
**Contras:** concorrência limitada, sem tipos enumerados nem `inet`; pouco
representativo de um sistema de saúde real numa banca.

## Análise de trade-offs

A e B atendem tecnicamente. PostgreSQL vence por custo zero sem restrição de uso,
`jsonb` e índices parciais que expressam regras do domínio diretamente no banco.
SQLite fica restrito a testes automatizados, se útil.

## Consequências

- Mais fácil: regras de integridade declaradas no banco; nenhuma licença.
- Mais difícil: tipos enumerados do PostgreSQL exigem mapeamento no Npgsql
  (alternativa: armazenar como `varchar` com `CHECK`, decidir na implementação).
- Revisitar: hospedagem caso o sistema seja usado fora do ambiente local.

## Ações

1. [ ] Criar o `AppDbContext` e a primeira migration a partir do doc 05.
2. [ ] Adicionar `.env.example` com a connection string de exemplo (sem segredo real).
