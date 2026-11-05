using Dermatoglifia.Aplicacao.Analises;
using Dermatoglifia.Aplicacao.Auditoria;
using Dermatoglifia.Aplicacao.Autenticacao;
using Dermatoglifia.Aplicacao.Coletas;
using Dermatoglifia.Aplicacao.Medicos;
using Dermatoglifia.Aplicacao.Modelos;
using Dermatoglifia.Aplicacao.Pacientes;
using Microsoft.Extensions.DependencyInjection;

namespace Dermatoglifia.Aplicacao;

public static class InjecaoDeDependencia
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services)
    {
        services.AddScoped<ServicoAutenticacao>();
        services.AddScoped<ServicoMedicos>();
        services.AddScoped<ServicoPacientes>();
        services.AddScoped<ServicoColetas>();
        services.AddScoped<ServicoAnalises>();
        services.AddScoped<ServicoModelos>();
        services.AddScoped<ServicoAuditoria>();
        return services;
    }
}
