using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Aplicacao.Autenticacao;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dermatoglifia.Api.Autenticacao;

/// <summary>Nomes das claims e políticas usadas no token (ADR-0006).</summary>
public static class Claims
{
    public const string Id = "sub";
    public const string Nome = "nome";
    public const string Papel = "papel";
    public const string ResumoCarimbo = "sst";
}

public static class Politicas
{
    public const string ApenasAdmin = nameof(ApenasAdmin);
    public const string ApenasMedico = nameof(ApenasMedico);
    public const string LimiteEntrada = "limite-entrada";
}

/// <summary>Configuração <c>Jwt:*</c>. A chave nunca fica no Git (user-secrets ou variável de ambiente).</summary>
public sealed class OpcoesJwt
{
    public const int TamanhoMinimoChaveBytes = 32;

    public string Chave { get; set; } = string.Empty;
    public string Emissor { get; set; } = string.Empty;
    public string Audiencia { get; set; } = string.Empty;
    public int ValidadeMinutos { get; set; } = 60;

    /// <summary>Lê e valida na inicialização: sem chave adequada, a API não sobe.</summary>
    public static OpcoesJwt Carregar(IConfiguration configuracao)
    {
        var opcoes = configuracao.GetSection("Jwt").Get<OpcoesJwt>() ?? new OpcoesJwt();

        byte[] chave;
        try
        {
            chave = Convert.FromBase64String(opcoes.Chave);
        }
        catch (FormatException)
        {
            chave = [];
        }

        if (chave.Length < TamanhoMinimoChaveBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Chave ausente ou curta: informe pelo menos {TamanhoMinimoChaveBytes} bytes aleatórios em base64 " +
                "(ex.: openssl rand -base64 32) via 'dotnet user-secrets' ou a variável Jwt__Chave.");
        }

        if (string.IsNullOrWhiteSpace(opcoes.Emissor) || string.IsNullOrWhiteSpace(opcoes.Audiencia) || opcoes.ValidadeMinutos <= 0)
        {
            throw new InvalidOperationException("Jwt:Emissor, Jwt:Audiencia e Jwt:ValidadeMinutos precisam estar configurados.");
        }

        return opcoes;
    }

    public SymmetricSecurityKey ChaveAssinatura() => new(Convert.FromBase64String(Chave));
}

/// <summary>Emite o JWT de acesso.</summary>
public sealed class EmissorTokenJwt(OpcoesJwt opcoes, IRelogio relogio) : IEmissorToken
{
    private static readonly JsonWebTokenHandler Manipulador = new();

    public TokenEmitido Emitir(ContaAutenticada conta)
    {
        ArgumentNullException.ThrowIfNull(conta);

        var agora = relogio.Agora;
        var expiraEm = agora.AddMinutes(opcoes.ValidadeMinutos);
        var descritor = new SecurityTokenDescriptor
        {
            Issuer = opcoes.Emissor,
            Audience = opcoes.Audiencia,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [Claims.Id] = conta.Id.ToString(),
                [Claims.Nome] = conta.Nome,
                [Claims.Papel] = EnumComoTexto.ParaTexto(conta.Papel),
                [Claims.ResumoCarimbo] = CarimboSeguranca.Resumo(conta.CarimboSeguranca),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(opcoes.ChaveAssinatura(), SecurityAlgorithms.HmacSha256),
        };

        return new TokenEmitido(Manipulador.CreateToken(descritor), expiraEm);
    }
}

/// <summary>Usuário da requisição atual, lido do token.</summary>
public sealed class UsuarioAtualHttp(IHttpContextAccessor acessor) : IUsuarioAtual
{
    private ClaimsPrincipal? Principal => acessor.HttpContext?.User;

    public Guid? Id => Guid.TryParse(Principal?.FindFirstValue(Claims.Id), out var id) ? id : null;

    public PapelUsuario? Papel => Principal?.FindFirstValue(Claims.Papel) is { } papel
        ? EnumComoTexto.DeTexto<PapelUsuario>(papel)
        : null;

    public IPAddress? Ip => acessor.HttpContext?.Connection.RemoteIpAddress;
}

public static class ConfiguracaoAutenticacao
{
    public static IServiceCollection AdicionarAutenticacaoJwt(this IServiceCollection services, IConfiguration configuracao)
    {
        var opcoes = OpcoesJwt.Carregar(configuracao);
        services.AddSingleton(opcoes);
        services.AddSingleton<IEmissorToken, EmissorTokenJwt>();
        services.AddHttpContextAccessor();
        services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = opcoes.Emissor,
                    ValidAudience = opcoes.Audiencia,
                    IssuerSigningKey = opcoes.ChaveAssinatura(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = Claims.Nome,
                    RoleClaimType = Claims.Papel,
                };
                jwt.Events = new JwtBearerEvents
                {
                    // Conta inativada/recusada ou stamp trocado invalida o token na hora.
                    OnTokenValidated = async contexto =>
                    {
                        var principal = contexto.Principal;
                        var valido =
                            Guid.TryParse(principal?.FindFirstValue(Claims.Id), out var id)
                            && principal?.FindFirstValue(Claims.ResumoCarimbo) is { } resumo
                            && await contexto.HttpContext.RequestServices.GetRequiredService<IContas>()
                                .SessaoValidaAsync(id, resumo, contexto.HttpContext.RequestAborted);
                        if (!valido)
                        {
                            contexto.Fail("Sessão inválida.");
                        }
                    },
                };
            });

        services.AddAuthorizationBuilder()
            // Por padrão, toda rota exige login; rotas públicas usam [AllowAnonymous].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Politicas.ApenasAdmin, p => p.RequireClaim(Claims.Papel, EnumComoTexto.ParaTexto(PapelUsuario.Admin)))
            .AddPolicy(Politicas.ApenasMedico, p => p.RequireClaim(Claims.Papel, EnumComoTexto.ParaTexto(PapelUsuario.Medico)));

        // Limite por IP nas rotas públicas (login e solicitação de cadastro).
        var limitePorMinuto = configuracao.GetValue("LimiteRequisicoes:EntradaPorMinuto", 10);
        services.AddRateLimiter(limitador =>
        {
            limitador.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limitador.AddPolicy(Politicas.LimiteEntrada, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limitePorMinuto,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }
}
