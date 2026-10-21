using System.ComponentModel.DataAnnotations;
using Dermatoglifia.Api.Autenticacao;
using Dermatoglifia.Aplicacao.Medicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dermatoglifia.Api.Controllers;

/// <summary>UC02 — Solicitar cadastro de médico; UC03 — Aprovar cadastro de médico.</summary>
[ApiController]
[Route("api/medicos")]
public sealed class MedicosController(ServicoMedicos servico) : ControllerBase
{
    [HttpPost("solicitacoes")]
    [AllowAnonymous]
    [EnableRateLimiting(Politicas.LimiteEntrada)]
    [ProducesResponseType<RespostaSolicitacao>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Solicitar(RequisicaoSolicitacao requisicao, CancellationToken cancelamento)
    {
        var id = await servico.SolicitarCadastroAsync(
            new SolicitacaoCadastro(
                requisicao.Nome, requisicao.Email, requisicao.Senha,
                requisicao.Crm, requisicao.UfCrm, requisicao.Especialidade),
            cancelamento);

        return StatusCode(StatusCodes.Status201Created, new RespostaSolicitacao(id, "PENDENTE"));
    }

    [HttpGet("solicitacoes")]
    [Authorize(Policy = Politicas.ApenasAdmin)]
    public Task<IReadOnlyList<SolicitacaoPendente>> ListarPendentes(CancellationToken cancelamento)
        => servico.ListarPendentesAsync(cancelamento);

    [HttpPost("{id:guid}/aprovacao")]
    [Authorize(Policy = Politicas.ApenasAdmin)]
    public async Task<IActionResult> Aprovar(Guid id, RequisicaoAprovacao requisicao, CancellationToken cancelamento)
    {
        await servico.AprovarAsync(id, requisicao.VinculoProtocolo, cancelamento);
        return NoContent();
    }

    [HttpPost("{id:guid}/recusa")]
    [Authorize(Policy = Politicas.ApenasAdmin)]
    public async Task<IActionResult> Recusar(Guid id, RequisicaoRecusa requisicao, CancellationToken cancelamento)
    {
        await servico.RecusarAsync(id, requisicao.Motivo, cancelamento);
        return NoContent();
    }
}

public sealed record RequisicaoSolicitacao(
    [Required(ErrorMessage = "Informe o nome."), StringLength(150, ErrorMessage = "Nome com no máximo 150 caracteres.")]
    string Nome,
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), StringLength(256)]
    string Email,
    [Required(ErrorMessage = "Informe a senha."), StringLength(128, ErrorMessage = "Senha com no máximo 128 caracteres.")]
    string Senha,
    [Required(ErrorMessage = "Informe o CRM.")] string Crm,
    [Required(ErrorMessage = "Informe a UF do CRM.")] string UfCrm,
    [Required(ErrorMessage = "Informe a especialidade.")] string Especialidade);

public sealed record RespostaSolicitacao(Guid Id, string Status);

public sealed record RequisicaoAprovacao(
    [Required(ErrorMessage = "Informe o vínculo com o protocolo de pesquisa.")] string VinculoProtocolo);

public sealed record RequisicaoRecusa(
    [Required(ErrorMessage = "Informe o motivo da recusa.")] string Motivo);
