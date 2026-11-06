using System.ComponentModel.DataAnnotations;
using Dermatoglifia.Api.Autenticacao;
using Dermatoglifia.Aplicacao.Pacientes;
using Dermatoglifia.Dominio.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dermatoglifia.Api.Controllers;

/// <summary>
/// UC04 — Cadastrar paciente; UC05 — Registrar consentimento; UC13 — Atualizar
/// situação diagnóstica; UC12 — Atender pedido do titular. Cada ação declara a
/// própria política (ADR-0010): ler e revogar termo são do médico (os pacientes
/// dele) ou do administrador (todos); cadastrar e registrar dados clínicos, só do
/// médico; exportar, corrigir identificação e eliminar, só do administrador.
/// </summary>
[ApiController]
[Route("api/pacientes")]
public sealed class PacientesController(ServicoPacientes servico, ServicoTitular titular) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Politicas.ApenasMedico)]
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
    [Authorize(Policy = Politicas.MedicoOuAdmin)]
    public Task<IReadOnlyList<PacienteResumo>> Listar([FromQuery, StringLength(100)] string? busca, CancellationToken cancelamento)
        => servico.ListarAsync(busca, cancelamento);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Politicas.MedicoOuAdmin)]
    public Task<PacienteDetalhe> Obter(Guid id, CancellationToken cancelamento)
        => servico.ObterAsync(id, cancelamento);

    [HttpPost("{id:guid}/consentimentos")]
    [Authorize(Policy = Politicas.ApenasMedico)]
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

    [HttpPatch("{id:guid}/situacao-diagnostica")]
    [Authorize(Policy = Politicas.ApenasMedico)]
    public Task<SituacaoAtualizada> AtualizarSituacao(Guid id, RequisicaoSituacao requisicao, CancellationToken cancelamento)
        => servico.AtualizarSituacaoAsync(
            id,
            new NovaSituacao(requisicao.Situacao!.Value, requisicao.Fonte, requisicao.AvaliadoEm, requisicao.IndependenteDoSistema),
            cancelamento);

    [HttpPost("{id:guid}/consentimentos/{consentimentoId:guid}/revogacao")]
    [Authorize(Policy = Politicas.MedicoOuAdmin)]
    public Task<ResultadoConsentimento> RevogarConsentimento(Guid id, Guid consentimentoId, CancellationToken cancelamento)
        => servico.RevogarConsentimentoAsync(id, consentimentoId, cancelamento);

    /// <summary>UC12 — pacote .zip com os dados e as imagens do paciente (LGPD art. 18, II e V).</summary>
    [HttpGet("{id:guid}/exportacao")]
    [Authorize(Policy = Politicas.ApenasAdmin)]
    public async Task<IActionResult> Exportar(Guid id, CancellationToken cancelamento)
    {
        var arquivo = await titular.ExportarAsync(id, cancelamento);
        return File(arquivo.Conteudo, "application/zip", arquivo.Nome);
    }

    /// <summary>UC12 — corrige a identificação (LGPD art. 18, III).</summary>
    [HttpPut("{id:guid}/identificacao")]
    [Authorize(Policy = Politicas.ApenasAdmin)]
    public async Task<IActionResult> CorrigirIdentificacao(Guid id, RequisicaoIdentificacao requisicao, CancellationToken cancelamento)
    {
        var responsavel = requisicao.Responsavel is null
            ? null
            : new NovoResponsavel(requisicao.Responsavel.Nome, requisicao.Responsavel.Parentesco, requisicao.Responsavel.Contato);
        await titular.CorrigirIdentificacaoAsync(
            id, new NovaIdentificacao(requisicao.Nome, requisicao.DataNascimento!.Value, responsavel), cancelamento);
        return NoContent();
    }

    /// <summary>UC12 — elimina identificação, imagens e textos livres (LGPD art. 18, VI; RN08). Irreversível.</summary>
    [HttpPost("{id:guid}/eliminacao")]
    [Authorize(Policy = Politicas.ApenasAdmin)]
    public async Task<RespostaEliminacao> Eliminar(Guid id, RequisicaoEliminacao requisicao, CancellationToken cancelamento)
        => new(await titular.EliminarAsync(id, requisicao.Confirmacao, cancelamento));
}

public sealed record RequisicaoIdentificacao(
    [Required(ErrorMessage = "Informe o nome."), StringLength(150, ErrorMessage = "Nome com no máximo 150 caracteres.")]
    string Nome,
    [Required(ErrorMessage = "Informe a data de nascimento.")] DateOnly? DataNascimento,
    RequisicaoResponsavel? Responsavel);

public sealed record RequisicaoEliminacao(
    [Required(ErrorMessage = "Digite ELIMINAR para confirmar.")] string Confirmacao);

public sealed record RespostaEliminacao(int ImagensApagadas);

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

public sealed record RequisicaoSituacao(
    [Required(ErrorMessage = "Informe a situação diagnóstica.")] SituacaoDiagnostica? Situacao,
    [StringLength(200, ErrorMessage = "Fonte com no máximo 200 caracteres.")] string? Fonte,
    DateOnly? AvaliadoEm,
    bool IndependenteDoSistema);
