using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;

namespace Dermatoglifia.Api.Erros;

/// <summary>
/// Toda resposta de erro sai em <c>application/problem+json</c> com título em
/// português (doc 08, seção 5) — inclusive as que o próprio ASP.NET Core gera sem
/// corpo (401, 403, 404, 405, 415, 429). Títulos definidos pelo código (regras de
/// negócio, validação) são mantidos; só os títulos padrão em inglês são trocados.
/// </summary>
public static class ConfiguracaoProblemas
{
    private static readonly Dictionary<int, string> TitulosPadrao = new()
    {
        [StatusCodes.Status400BadRequest] = "Requisição inválida.",
        [StatusCodes.Status401Unauthorized] = "Autenticação necessária. Faça login novamente.",
        [StatusCodes.Status403Forbidden] = "Acesso não permitido para o seu perfil.",
        [StatusCodes.Status404NotFound] = "Recurso não encontrado.",
        [StatusCodes.Status405MethodNotAllowed] = "Método não permitido para esta rota.",
        [StatusCodes.Status409Conflict] = "Conflito com o estado atual do registro.",
        [StatusCodes.Status413PayloadTooLarge] = "Conteúdo acima do tamanho permitido.",
        [StatusCodes.Status415UnsupportedMediaType] = "Tipo de conteúdo não suportado.",
        [StatusCodes.Status422UnprocessableEntity] = "Operação não permitida pelas regras do sistema.",
        [StatusCodes.Status429TooManyRequests] = "Muitas tentativas. Aguarde um minuto e tente novamente.",
        [StatusCodes.Status500InternalServerError] = "Erro inesperado. Tente novamente.",
    };

    /// <summary>Título genérico que o ASP.NET Core usa para 500.</summary>
    private const string TituloGenerico500 = "An error occurred while processing your request.";

    public static IServiceCollection AdicionarProblemas(this IServiceCollection services)
    {
        services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
        {
            var problema = contexto.ProblemDetails;
            var status = problema.Status ?? contexto.HttpContext.Response.StatusCode;
            var tituloPadrao = ReasonPhrases.GetReasonPhrase(status);

            if (TitulosPadrao.TryGetValue(status, out var titulo)
                && (string.IsNullOrEmpty(problema.Title) || problema.Title == tituloPadrao || problema.Title == TituloGenerico500))
            {
                problema.Title = titulo;
            }

            // Detalhes internos nunca saem na resposta; o traceId fica para cruzar com o log.
            problema.Detail = status >= StatusCodes.Status500InternalServerError ? null : problema.Detail;
            problema.Extensions.Remove("exception");
            problema.Extensions["traceId"] = contexto.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity.Id
                                             ?? contexto.HttpContext.TraceIdentifier;
        });

        services.AddExceptionHandler<TratadorExcecoes>();
        return services;
    }
}
