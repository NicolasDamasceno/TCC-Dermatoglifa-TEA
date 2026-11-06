using System.ComponentModel.DataAnnotations;
using Dermatoglifia.Api.Autenticacao;
using Dermatoglifia.Aplicacao.Analises;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dermatoglifia.Api.Controllers;

/// <summary>
/// UC07 (nova tentativa) e UC08 — resultado da análise. As ações são do médico que
/// conduziu a coleta; o administrador só lê (ADR-0010). Todo resultado traz o aviso
/// de protótipo de pesquisa (RN04).
/// </summary>
[ApiController]
[Route("api/analises")]
public sealed class AnalisesController(ServicoAnalises servico) : ControllerBase
{
    // Cada ação declara a própria política: ler é do médico ou do administrador;
    // agir sobre o resultado é só do médico (ADR-0010).
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Politicas.MedicoOuAdmin)]
    public Task<AnaliseDto> Obter(Guid id, CancellationToken cancelamento) => servico.ObterAsync(id, cancelamento);

    [HttpPost("{id:guid}/nova-tentativa")]
    [Authorize(Policy = Politicas.ApenasMedico)]
    public Task<AnaliseDto> NovaTentativa(Guid id, CancellationToken cancelamento)
        => servico.NovaTentativaAsync(id, cancelamento);

    [HttpPatch("{id:guid}/observacao")]
    [Authorize(Policy = Politicas.ApenasMedico)]
    public Task<AnaliseDto> RegistrarObservacao(Guid id, RequisicaoObservacao requisicao, CancellationToken cancelamento)
        => servico.RegistrarObservacaoAsync(id, requisicao.Observacao, cancelamento);

    /// <summary>Confirma que o uso da IA foi registrado no prontuário (Res. CFM 2.454/2026).</summary>
    [HttpPost("{id:guid}/registro-prontuario")]
    [Authorize(Policy = Politicas.ApenasMedico)]
    public Task<AnaliseDto> ConfirmarRegistroProntuario(Guid id, CancellationToken cancelamento)
        => servico.ConfirmarRegistroProntuarioAsync(id, cancelamento);
}

public sealed record RequisicaoObservacao(
    [StringLength(2000, ErrorMessage = "Observação com no máximo 2000 caracteres.")] string? Observacao);
