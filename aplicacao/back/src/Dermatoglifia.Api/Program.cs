using System.Text.Json;
using System.Text.Json.Serialization;
using Dermatoglifia.Api.Autenticacao;
using Dermatoglifia.Api.Erros;
using Dermatoglifia.Aplicacao;
using Dermatoglifia.Infraestrutura;
using Dermatoglifia.Infraestrutura.Identidade;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(json =>
        // Enums em MAIÚSCULAS_COM_SUBLINHADO, igual ao banco (doc 08, seção 5).
        json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)))
    .ConfigureApiBehaviorOptions(api =>
        api.InvalidModelStateResponseFactory = contexto =>
        {
            var problema = new ValidationProblemDetails(contexto.ModelState)
            {
                Title = "Dados inválidos",
                Status = StatusCodes.Status400BadRequest,
            };
            return new BadRequestObjectResult(problema);
        });

builder.Services.AdicionarProblemas();
builder.Services.AdicionarAplicacao();
builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarAutenticacaoJwt(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(); // 401, 403, 404, 405, 429... sem corpo ganham problem+json
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.Services.CriarAdminInicialAsync(app.Configuration);
await app.RunAsync();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
