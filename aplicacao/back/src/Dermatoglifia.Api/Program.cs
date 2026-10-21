using Dermatoglifia.Infraestrutura;

var builder = WebApplication.CreateBuilder(args);

// Serviços da aplicação. Banco (M1), Identity + JWT (M2) e demais casos de uso
// entram aqui conforme documentacao/08-back-end.md, seção 11.
builder.Services.AddControllers();
builder.Services.AdicionarInfraestrutura(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
