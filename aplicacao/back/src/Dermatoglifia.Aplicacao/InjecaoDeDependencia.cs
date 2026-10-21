using Dermatoglifia.Aplicacao.Autenticacao;
using Dermatoglifia.Aplicacao.Medicos;
using Microsoft.Extensions.DependencyInjection;

namespace Dermatoglifia.Aplicacao;

public static class InjecaoDeDependencia
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services)
    {
        services.AddScoped<ServicoAutenticacao>();
        services.AddScoped<ServicoMedicos>();
        return services;
    }
}
