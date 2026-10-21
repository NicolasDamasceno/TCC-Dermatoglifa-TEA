using System.ComponentModel.DataAnnotations;
using Dermatoglifia.Api.Autenticacao;
using Dermatoglifia.Aplicacao.Autenticacao;
using Dermatoglifia.Dominio.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dermatoglifia.Api.Controllers;

/// <summary>UC01 — Autenticar-se.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AutenticacaoController(ServicoAutenticacao servico) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(Politicas.LimiteEntrada)]
    [ProducesResponseType<RespostaEntrada>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Entrar(RequisicaoEntrada requisicao, CancellationToken cancelamento)
    {
        var resultado = await servico.EntrarAsync(requisicao.Email, requisicao.Senha, cancelamento);
        if (resultado.Sucedeu)
        {
            var conta = resultado.Conta!;
            return Ok(new RespostaEntrada(
                resultado.Token!.Token,
                resultado.Token.ExpiraEm,
                new UsuarioEntrada(conta.Id, conta.Nome, conta.Papel)));
        }

        // Mensagens genéricas: não revelam se o e-mail existe (UC01, fluxo 2a).
        return resultado.Motivo switch
        {
            MotivoFalhaEntrada.Bloqueada => Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Muitas tentativas sem sucesso. Tente novamente em alguns minutos."),
            MotivoFalhaEntrada.CadastroPendente => Problem(
                statusCode: StatusCodes.Status403Forbidden, title: "Cadastro aguardando aprovação."),
            MotivoFalhaEntrada.CadastroRecusado => Problem(
                statusCode: StatusCodes.Status403Forbidden, title: "Cadastro recusado."),
            MotivoFalhaEntrada.CadastroInativo => Problem(
                statusCode: StatusCodes.Status403Forbidden, title: "Cadastro inativo."),
            _ => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-mail ou senha incorretos."),
        };
    }
}

public sealed record RequisicaoEntrada(
    [Required(ErrorMessage = "Informe o e-mail.")] string Email,
    [Required(ErrorMessage = "Informe a senha.")] string Senha);

public sealed record RespostaEntrada(string Token, DateTimeOffset ExpiraEm, UsuarioEntrada Usuario);

public sealed record UsuarioEntrada(Guid Id, string Nome, PapelUsuario Papel);
