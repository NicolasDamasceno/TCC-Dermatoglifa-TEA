using Dermatoglifia.Aplicacao.Excecoes;
using Dermatoglifia.Dominio.Excecoes;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dermatoglifia.Api.Erros;

/// <summary>
/// Converte exceções em <c>application/problem+json</c> num único lugar (doc 08,
/// seção 5). Detalhes internos nunca vão para a resposta, só para o log.
/// </summary>
public sealed partial class TratadorExcecoes(IProblemDetailsService problemDetails, ILogger<TratadorExcecoes> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo) = exception switch
        {
            RegraDeNegocioException e => (StatusCodes.Status422UnprocessableEntity, e.Message),
            NaoEncontradoException e => (StatusCodes.Status404NotFound, e.Message),
            ConflitoException e => (StatusCodes.Status409Conflict, e.Message),
            FormatoNaoSuportadoException e => (StatusCodes.Status415UnsupportedMediaType, e.Message),
            ConteudoMuitoGrandeException e => (StatusCodes.Status413PayloadTooLarge, e.Message),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                (StatusCodes.Status413PayloadTooLarge, "Arquivo acima do tamanho permitido."),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "O registro foi alterado por outra operação. Recarregue e tente de novo."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "Registro duplicado."),
            _ => (StatusCodes.Status500InternalServerError, "Erro inesperado. Tente novamente."),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            LogErroInesperado(logger, exception);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status, Title = titulo },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro inesperado ao processar a requisição.")]
    private static partial void LogErroInesperado(ILogger logger, Exception excecao);
}
