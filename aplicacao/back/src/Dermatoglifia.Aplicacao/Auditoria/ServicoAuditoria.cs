using System.Globalization;
using System.Text.Json;
using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.Excecoes;
using Microsoft.EntityFrameworkCore;

namespace Dermatoglifia.Aplicacao.Auditoria;

/// <summary>
/// UC11 — Consultar trilha de auditoria. Só leitura: a tabela é somente inserção
/// (RNF07). A própria consulta também fica registrada. A trilha não guarda nomes de
/// pacientes: o paciente é filtrado pelo identificador pseudônimo.
/// </summary>
public sealed class ServicoAuditoria(IContextoDados contexto, IContas contas, IAuditoria auditoria)
{
    public const int TamanhoPadrao = 50;
    public const int TamanhoMaximo = 100;

    public async Task<PaginaAuditoria> ConsultarAsync(FiltroAuditoria filtro, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var tamanho = filtro.Tamanho ?? TamanhoPadrao;
        Regra(tamanho is >= 1 and <= TamanhoMaximo, $"Tamanho da página deve ser de 1 a {TamanhoMaximo}.");
        Regra(filtro.Pagina >= 1, "A página começa em 1.");
        Regra(filtro.De is null || filtro.Ate is null || filtro.De < filtro.Ate, "O início do período deve ser antes do fim.");

        var consulta = contexto.TrilhaAuditoria;
        if (filtro.UsuarioId is { } usuarioId)
        {
            consulta = consulta.Where(r => r.UsuarioId == usuarioId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Acao))
        {
            var acao = filtro.Acao.Trim();
            consulta = consulta.Where(r => r.Acao == acao);
        }

        if (filtro.De is { } de)
        {
            consulta = consulta.Where(r => r.OcorridoEm >= de);
        }

        if (filtro.Ate is { } ate)
        {
            consulta = consulta.Where(r => r.OcorridoEm < ate);
        }

        if (filtro.PacienteId is { } pacienteId)
        {
            // A trilha de um paciente inclui as sessões de coleta e as análises dele.
            var sessoes = contexto.SessoesColeta.Where(s => s.PacienteId == pacienteId).Select(s => s.Id);
            var analises = contexto.Analises.Where(a => sessoes.Contains(a.SessaoColetaId)).Select(a => a.Id);
            consulta = consulta.Where(r => r.EntidadeId == pacienteId
                                           || (r.Entidade == "sessao_coleta" && sessoes.Contains(r.EntidadeId!.Value))
                                           || (r.Entidade == "analise" && analises.Contains(r.EntidadeId!.Value)));
        }

        var total = await consulta.CountAsync(cancelamento);
        var registros = await consulta
            .OrderByDescending(r => r.Id)
            .Skip((filtro.Pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(cancelamento);

        var usuarios = await contas.ObterResumosAsync(
            registros.Where(r => r.UsuarioId is not null).Select(r => r.UsuarioId!.Value), cancelamento);

        auditoria.Registrar("AUDITORIA_CONSULTADA", "log_auditoria", null,
            new Dictionary<string, string> { ["quantidade"] = total.ToString(CultureInfo.InvariantCulture) });
        await contexto.SalvarAsync(cancelamento);

        var itens = registros.Select(r =>
        {
            var usuario = r.UsuarioId is { } id && usuarios.TryGetValue(id, out var conta)
                ? new UsuarioAuditoria(conta.Id, conta.Nome, conta.Email, conta.Papel)
                : null;
            return new RegistroAuditoriaDto(
                r.Id, r.OcorridoEm, r.UsuarioId, usuario, r.Acao, r.Entidade, r.EntidadeId, r.Ip?.ToString(),
                Detalhes(r.Detalhes));
        }).ToList();

        return new PaginaAuditoria(itens, total, filtro.Pagina, tamanho);
    }

    /// <summary>Opções dos filtros: ações já registradas e as contas existentes.</summary>
    public async Task<FiltrosAuditoria> ObterFiltrosAsync(CancellationToken cancelamento = default)
    {
        var acoes = await contexto.TrilhaAuditoria
            .Select(r => r.Acao)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(cancelamento);
        var usuarios = await contas.ListarAsync(cancelamento);
        return new FiltrosAuditoria(acoes, usuarios.Select(u => new UsuarioAuditoria(u.Id, u.Nome, u.Email, u.Papel)).ToList());
    }

    private static JsonElement? Detalhes(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var documento = JsonDocument.Parse(json);
        return documento.RootElement.Clone();
    }

    private static void Regra(bool condicao, string mensagem)
    {
        if (!condicao)
        {
            throw new RegraDeNegocioException(mensagem);
        }
    }
}

public sealed record FiltroAuditoria(
    Guid? UsuarioId, Guid? PacienteId, string? Acao, DateTimeOffset? De, DateTimeOffset? Ate, int Pagina = 1, int? Tamanho = null);

public sealed record UsuarioAuditoria(Guid Id, string Nome, string Email, PapelUsuario Papel);

/// <param name="Usuario">Nulo em eventos sem usuário (falha de login com e-mail inexistente, admin inicial).</param>
public sealed record RegistroAuditoriaDto(
    long Id,
    DateTimeOffset OcorridoEm,
    Guid? UsuarioId,
    UsuarioAuditoria? Usuario,
    string Acao,
    string Entidade,
    Guid? EntidadeId,
    string? Ip,
    JsonElement? Detalhes);

public sealed record PaginaAuditoria(IReadOnlyList<RegistroAuditoriaDto> Itens, int Total, int Pagina, int Tamanho);

public sealed record FiltrosAuditoria(IReadOnlyList<string> Acoes, IReadOnlyList<UsuarioAuditoria> Usuarios);
