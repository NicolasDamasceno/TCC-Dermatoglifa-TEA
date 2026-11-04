using System.ComponentModel.DataAnnotations;
using Dermatoglifia.Api.Autenticacao;
using Dermatoglifia.Aplicacao.Analises;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dermatoglifia.Api.Controllers;

/// <summary>
/// UC07 (nova tentativa) e UC08 — resultado da análise. Só o médico que conduziu a
/// coleta. Todo resultado traz o aviso de protótipo de pesquisa (RN04).
/// </summary>
[ApiController]
[Route("api/analises")]
[Authorize(Policy = Politicas.ApenasMedico)]
public sealed class AnalisesController(ServicoAnalises servico) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<AnaliseDto> Obter(Guid id, CancellationToken cancelamento) => servico.ObterAsync(id, cancelamento);

    [HttpPost("{id:guid}/nova-tentativa")]
    public Task<AnaliseDto> NovaTentativa(Guid id, CancellationToken cancelamento)
        => servico.NovaTentativaAsync(id, cancelamento);

    [HttpPatch("{id:guid}/observacao")]
    public Task<AnaliseDto> RegistrarObservacao(Guid id, RequisicaoObservacao requisicao, CancellationToken cancelamento)
        => servico.RegistrarObservacaoAsync(id, requisicao.Observacao, cancelamento);

    /// <summary>Confirma que o uso da IA foi registrado no prontuário (Res. CFM 2.454/2026).</summary>
    [HttpPost("{id:guid}/registro-prontuario")]
    public Task<AnaliseDto> ConfirmarRegistroProntuario(Guid id, CancellationToken cancelamento)
        => servico.ConfirmarRegistroProntuarioAsync(id, cancelamento);
}

public sealed record RequisicaoObservacao(
    [StringLength(2000, ErrorMessage = "Observação com no máximo 2000 caracteres.")] string? Observacao);
