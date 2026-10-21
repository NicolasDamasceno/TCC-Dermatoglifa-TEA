using System.ComponentModel.DataAnnotations;
using Dermatoglifia.Api.Autenticacao;
using Dermatoglifia.Aplicacao.Pacientes;
using Dermatoglifia.Dominio.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dermatoglifia.Api.Controllers;

/// <summary>
/// UC04 — Cadastrar paciente; UC05 — Registrar consentimento. Só médicos acessam;
/// o administrador não vê dados de pacientes.
/// </summary>
[ApiController]
[Route("api/pacientes")]
[Authorize(Policy = Politicas.ApenasMedico)]
public sealed class PacientesController(ServicoPacientes servico) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<RespostaPacienteCriado>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Cadastrar(RequisicaoPaciente requisicao, CancellationToken cancelamento)
    {
        var responsavel = requisicao.Responsavel is null
            ? null
            : new NovoResponsavel(requisicao.Responsavel.Nome, requisicao.Responsavel.Parentesco, requisicao.Responsavel.Contato);

        var id = await servico.CadastrarAsync(
            new NovoPaciente(requisicao.Nome, requisicao.DataNascimento!.Value, requisicao.Sexo!.Value, responsavel),
            cancelamento);

        return CreatedAtAction(nameof(Obter), new { id }, new RespostaPacienteCriado(id, PodeColetar: false));
    }

    [HttpGet]
    public Task<IReadOnlyList<PacienteResumo>> Listar([FromQuery, StringLength(100)] string? busca, CancellationToken cancelamento)
        => servico.ListarAsync(busca, cancelamento);

    [HttpGet("{id:guid}")]
    public Task<PacienteDetalhe> Obter(Guid id, CancellationToken cancelamento)
        => servico.ObterAsync(id, cancelamento);

    [HttpPost("{id:guid}/consentimentos")]
    [ProducesResponseType<ResultadoConsentimento>(StatusCodes.Status201Created)]
    public async Task<IActionResult> RegistrarConsentimento(Guid id, RequisicaoConsentimento requisicao, CancellationToken cancelamento)
    {
        var resultado = await servico.RegistrarConsentimentoAsync(
            id,
            new NovoConsentimento(
                requisicao.Tipo!.Value, requisicao.VersaoTermo, requisicao.AssinadoEm!.Value,
                requisicao.AutorizaAnalise, requisicao.CienteUsoIA, requisicao.AutorizaUsoTreino),
            cancelamento);

        return StatusCode(StatusCodes.Status201Created, resultado);
    }

    [HttpPost("{id:guid}/consentimentos/{consentimentoId:guid}/revogacao")]
    public Task<ResultadoConsentimento> RevogarConsentimento(Guid id, Guid consentimentoId, CancellationToken cancelamento)
        => servico.RevogarConsentimentoAsync(id, consentimentoId, cancelamento);
}

public sealed record RequisicaoPaciente(
    [Required(ErrorMessage = "Informe o nome."), StringLength(150, ErrorMessage = "Nome com no máximo 150 caracteres.")]
    string Nome,
    [Required(ErrorMessage = "Informe a data de nascimento.")] DateOnly? DataNascimento,
    [Required(ErrorMessage = "Informe o sexo biológico.")] SexoBiologico? Sexo,
    RequisicaoResponsavel? Responsavel);

public sealed record RequisicaoResponsavel(
    [StringLength(150)] string? Nome,
    [StringLength(50)] string? Parentesco,
    [StringLength(100)] string? Contato);

public sealed record RespostaPacienteCriado(Guid Id, bool PodeColetar);

public sealed record RequisicaoConsentimento(
    [Required(ErrorMessage = "Informe o tipo de termo (TCLE ou TALE).")] TipoTermo? Tipo,
    [Required(ErrorMessage = "Informe a versão do termo aprovada pelo CEP."), StringLength(50)] string VersaoTermo,
    [Required(ErrorMessage = "Informe a data de assinatura.")] DateOnly? AssinadoEm,
    bool AutorizaAnalise,
    bool CienteUsoIA,
    bool AutorizaUsoTreino);
