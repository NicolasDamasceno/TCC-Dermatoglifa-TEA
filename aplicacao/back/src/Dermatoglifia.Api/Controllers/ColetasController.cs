using System.ComponentModel.DataAnnotations;
using Dermatoglifia.Api.Autenticacao;
using Dermatoglifia.Aplicacao.Coletas;
using Dermatoglifia.Dominio.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dermatoglifia.Api.Controllers;

/// <summary>UC06 — Coletar as 10 digitais. Só o médico responsável pelo paciente.</summary>
[ApiController]
[Route("api")]
[Authorize(Policy = Politicas.ApenasMedico)]
public sealed class ColetasController(ServicoColetas servico) : ControllerBase
{
    /// <summary>Margem para os cabeçalhos do multipart além dos 5 MB da imagem.</summary>
    private const long LimiteRequisicaoBytes = ServicoColetas.TamanhoMaximoBytes + (64 * 1024);

    [HttpPost("pacientes/{pacienteId:guid}/sessoes")]
    [ProducesResponseType<SessaoDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Iniciar(Guid pacienteId, RequisicaoSessao requisicao, CancellationToken cancelamento)
    {
        var sessao = await servico.IniciarAsync(
            pacienteId,
            new NovaSessao(requisicao.Metodo!.Value, requisicao.TipoImpressao!.Value, requisicao.Dispositivo),
            cancelamento);
        return CreatedAtAction(nameof(Obter), new { id = sessao.Id }, sessao);
    }

    [HttpGet("sessoes/{id:guid}")]
    public Task<SessaoDto> Obter(Guid id, CancellationToken cancelamento) => servico.ObterAsync(id, cancelamento);

    /// <summary>Envia ou refaz a imagem de um dedo (multipart, campo <c>imagem</c>).</summary>
    [HttpPut("sessoes/{id:guid}/digitais/{mao}/{dedo}")]
    [RequestSizeLimit(LimiteRequisicaoBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteRequisicaoBytes)]
    public async Task<SessaoDto> EnviarDigital(
        Guid id, Mao mao, Dedo dedo, [Required(ErrorMessage = "Envie o arquivo no campo 'imagem'.")] IFormFile imagem,
        CancellationToken cancelamento)
    {
        await using var conteudo = imagem.OpenReadStream();
        return await servico.EnviarDigitalAsync(id, mao, dedo, conteudo, imagem.Length, cancelamento);
    }

    [HttpPost("sessoes/{id:guid}/digitais/{mao}/{dedo}/nao-coletavel")]
    public Task<SessaoDto> MarcarNaoColetavel(
        Guid id, Mao mao, Dedo dedo, RequisicaoNaoColetavel requisicao, CancellationToken cancelamento)
        => servico.MarcarNaoColetavelAsync(id, mao, dedo, requisicao.Motivo, cancelamento);

    [HttpPost("sessoes/{id:guid}/conclusao")]
    public Task<SessaoDto> Concluir(Guid id, CancellationToken cancelamento) => servico.ConcluirAsync(id, cancelamento);

    [HttpPost("sessoes/{id:guid}/cancelamento")]
    public Task<SessaoDto> Cancelar(Guid id, CancellationToken cancelamento) => servico.CancelarAsync(id, cancelamento);
}

public sealed record RequisicaoSessao(
    [Required(ErrorMessage = "Informe o método de captura.")] MetodoCaptura? Metodo,
    [Required(ErrorMessage = "Informe o tipo de impressão (ROLADA ou PLANA).")] TipoImpressao? TipoImpressao,
    [StringLength(100)] string? Dispositivo);

public sealed record RequisicaoNaoColetavel(
    [Required(ErrorMessage = "Informe o motivo."), StringLength(200)] string Motivo);
