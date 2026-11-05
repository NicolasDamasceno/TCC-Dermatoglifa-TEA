using System.ComponentModel.DataAnnotations;
using Dermatoglifia.Api.Autenticacao;
using Dermatoglifia.Aplicacao.Modelos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dermatoglifia.Api.Controllers;

/// <summary>UC10 — Promover versão do modelo. Só o administrador.</summary>
[ApiController]
[Route("api/modelos")]
[Authorize(Policy = Politicas.ApenasAdmin)]
public sealed class ModelosController(ServicoModelos servico) : ControllerBase
{
    private const long LimiteRequisicaoBytes = (2 * ServicoModelos.TamanhoMaximoJson) + (64 * 1024);

    /// <summary>Versões registradas e o modelo que o Serviço de IA está usando agora.</summary>
    [HttpGet]
    public Task<PainelModelos> Obter(CancellationToken cancelamento) => servico.ObterAsync(cancelamento);

    /// <summary>
    /// Registra (ou reativa) a versão que o Serviço de IA carregou e a torna a única
    /// ativa. Multipart com <c>config</c> e <c>metricas</c>, os JSON da pasta da versão.
    /// </summary>
    [HttpPost("promocao")]
    [RequestSizeLimit(LimiteRequisicaoBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteRequisicaoBytes)]
    public async Task<ModeloDto> Promover(
        [Required(ErrorMessage = "Envie o config.json da versão no campo 'config'.")] IFormFile config,
        [Required(ErrorMessage = "Envie o metricas.json da versão no campo 'metricas'.")] IFormFile metricas,
        CancellationToken cancelamento)
        => await servico.PromoverAsync(
            new ArquivosVersao(await LerAsync(config, cancelamento), await LerAsync(metricas, cancelamento)), cancelamento);

    private static async Task<byte[]> LerAsync(IFormFile arquivo, CancellationToken cancelamento)
    {
        if (arquivo.Length > ServicoModelos.TamanhoMaximoJson)
        {
            return []; // o serviço recusa com a mensagem de tamanho
        }

        using var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria, cancelamento);
        return memoria.ToArray();
    }
}
