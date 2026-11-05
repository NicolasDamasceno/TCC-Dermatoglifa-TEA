using System.ComponentModel.DataAnnotations;
using Dermatoglifia.Api.Autenticacao;
using Dermatoglifia.Aplicacao.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dermatoglifia.Api.Controllers;

/// <summary>UC11 — Consultar trilha de auditoria. Só o administrador.</summary>
[ApiController]
[Route("api/auditoria")]
[Authorize(Policy = Politicas.ApenasAdmin)]
public sealed class AuditoriaController(ServicoAuditoria servico) : ControllerBase
{
    /// <summary>Eventos do mais recente ao mais antigo; <c>ate</c> é exclusivo.</summary>
    [HttpGet]
    public Task<PaginaAuditoria> Consultar(
        [FromQuery] Guid? usuarioId,
        [FromQuery] Guid? pacienteId,
        [FromQuery, StringLength(60)] string? acao,
        [FromQuery] DateTimeOffset? de,
        [FromQuery] DateTimeOffset? ate,
        [FromQuery, Range(1, int.MaxValue)] int pagina = 1,
        [FromQuery, Range(1, ServicoAuditoria.TamanhoMaximo)] int? tamanho = null,
        CancellationToken cancelamento = default)
        => servico.ConsultarAsync(new FiltroAuditoria(usuarioId, pacienteId, acao, de, ate, pagina, tamanho), cancelamento);

    [HttpGet("filtros")]
    public Task<FiltrosAuditoria> Filtros(CancellationToken cancelamento) => servico.ObterFiltrosAsync(cancelamento);
}
