namespace Dermatoglifia.Api.Configuracao;

/// <summary>CORS e cabeçalhos de segurança das respostas (doc 08, seção 9; doc 06, segurança).</summary>
public static class ConfiguracaoHttp
{
    public const string PoliticaCors = "front";

    /// <summary>
    /// Origens do front em <c>Cors:OrigensPermitidas</c>. Sem origens configuradas,
    /// só chamadas da mesma origem funcionam. Curinga (<c>*</c>) é recusado: a API
    /// devolve dados pessoais sensíveis.
    /// </summary>
    public static IServiceCollection AdicionarCors(this IServiceCollection services, IConfiguration configuracao)
    {
        var origens = configuracao.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [];
        foreach (var origem in origens)
        {
            if (origem.Contains('*', StringComparison.Ordinal)
                || !Uri.TryCreate(origem, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
                || uri.AbsolutePath != "/"
                || origem.EndsWith('/'))
            {
                throw new InvalidOperationException(
                    $"Cors:OrigensPermitidas contém \"{origem}\": informe origens exatas, como http://localhost:5173 " +
                    "(esquema, host e porta, sem caminho, barra final ou curinga).");
            }
        }

        services.AddCors(cors => cors.AddPolicy(PoliticaCors, politica => politica
            .WithOrigins(origens)
            .WithMethods("GET", "POST", "PUT", "PATCH")
            .WithHeaders("Authorization", "Content-Type")
            .WithExposedHeaders("Location")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
        return services;
    }

    /// <summary>
    /// As respostas carregam dados pessoais e de saúde: não podem ficar em cache
    /// (navegador, proxy) e não devem ser reinterpretadas pelo navegador.
    /// </summary>
    public static IApplicationBuilder UsarCabecalhosSeguranca(this IApplicationBuilder app)
        => app.Use((contexto, proximo) =>
        {
            contexto.Response.OnStarting(() =>
            {
                var cabecalhos = contexto.Response.Headers;
                cabecalhos.CacheControl = "no-store";
                cabecalhos.XContentTypeOptions = "nosniff";
                cabecalhos["Referrer-Policy"] = "no-referrer";
                return Task.CompletedTask;
            });
            return proximo(contexto);
        });
}
