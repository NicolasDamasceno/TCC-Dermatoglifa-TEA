using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Infraestrutura.Identidade;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Dermatoglifia.Api.Testes;

/// <summary>UC01 a UC03 de ponta a ponta, com dados sintéticos.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class CadastroEAcessoTestes(AmbienteApi ambiente)
{
    private const string SenhaMedico = "senhamedico123";

    private static object Solicitacao(string email, string crm, string senha = SenhaMedico) => new
    {
        nome = "Médica Sintética",
        email,
        senha,
        crm,
        ufCrm = "PI",
        especialidade = "Pediatria",
    };

    private static string EmailNovo() => $"medica-{Guid.NewGuid():N}@teste.local";

    private static string CrmNovo() => Random.Shared.Next(100_000, 999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private async Task<(Guid Id, string Email)> SolicitarAsync()
    {
        var email = EmailNovo();
        var resposta = await ambiente.Cliente().PostAsJsonAsync("/api/medicos/solicitacoes", Solicitacao(email, CrmNovo()), AmbienteApi.Json);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json);
        Assert.Equal("PENDENTE", corpo.GetProperty("status").GetString());
        return (corpo.GetProperty("id").GetGuid(), email);
    }

    private async Task<HttpClient> AdminAsync()
        => ambiente.Cliente(await ambiente.TokenAsync(AmbienteApi.EmailAdmin, AmbienteApi.SenhaAdmin));

    private async Task<(Guid Id, string Email)> MedicoAprovadoAsync()
    {
        var medico = await SolicitarAsync();
        var admin = await AdminAsync();
        var aprovacao = await admin.PostAsJsonAsync($"/api/medicos/{medico.Id}/aprovacao", new { vinculoProtocolo = "Equipe CAAE sintético" });
        Assert.Equal(HttpStatusCode.NoContent, aprovacao.StatusCode);
        return medico;
    }

    private static async Task<string?> TituloAsync(HttpResponseMessage resposta)
        => (await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json)).GetProperty("title").GetString();

    [Fact]
    public async Task Fluxo_completo_solicitacao_aprovacao_e_login_do_medico()
    {
        var medico = await SolicitarAsync();
        var admin = await AdminAsync();

        var pendentes = await admin.GetFromJsonAsync<JsonElement>("/api/medicos/solicitacoes", AmbienteApi.Json);
        Assert.Contains(pendentes.EnumerateArray(), s => s.GetProperty("id").GetGuid() == medico.Id);

        var aprovacao = await admin.PostAsJsonAsync($"/api/medicos/{medico.Id}/aprovacao", new { vinculoProtocolo = "Equipe CAAE sintético" });
        Assert.Equal(HttpStatusCode.NoContent, aprovacao.StatusCode);

        var entrada = await ambiente.EntrarAsync(medico.Email, SenhaMedico);
        Assert.Equal(HttpStatusCode.OK, entrada.StatusCode);
        var corpo = await entrada.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json);
        Assert.Equal("MEDICO", corpo.GetProperty("usuario").GetProperty("papel").GetString());

        var token = new JsonWebToken(corpo.GetProperty("token").GetString());
        Assert.Equal(medico.Id.ToString(), token.Subject);
        Assert.Equal("MEDICO", token.GetClaim("papel").Value);

        var depois = await admin.GetFromJsonAsync<JsonElement>("/api/medicos/solicitacoes", AmbienteApi.Json);
        Assert.DoesNotContain(depois.EnumerateArray(), s => s.GetProperty("id").GetGuid() == medico.Id);
    }

    [Fact]
    public async Task Token_nao_carrega_o_security_stamp_em_claro()
    {
        var medico = await MedicoAprovadoAsync();
        var token = new JsonWebToken(await ambiente.TokenAsync(medico.Email, SenhaMedico));

        var carimbo = await ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().Users
            .Where(u => u.Id == medico.Id).Select(u => u.SecurityStamp).SingleAsync());

        Assert.DoesNotContain(token.Claims, c => c.Value == carimbo);
    }

    [Fact]
    public async Task Medico_pendente_ou_recusado_nao_entra()
    {
        var pendente = await SolicitarAsync();
        var resposta = await ambiente.EntrarAsync(pendente.Email, SenhaMedico);
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Cadastro aguardando aprovação.", await TituloAsync(resposta));

        var admin = await AdminAsync();
        var recusa = await admin.PostAsJsonAsync($"/api/medicos/{pendente.Id}/recusa", new { motivo = "Não pertence ao protocolo" });
        Assert.Equal(HttpStatusCode.NoContent, recusa.StatusCode);

        resposta = await ambiente.EntrarAsync(pendente.Email, SenhaMedico);
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Cadastro recusado.", await TituloAsync(resposta));
    }

    [Fact]
    public async Task Status_da_conta_so_e_revelado_com_a_senha_certa()
    {
        var pendente = await SolicitarAsync();

        var resposta = await ambiente.EntrarAsync(pendente.Email, "senhaerrada123");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Email_inexistente_e_senha_errada_tem_a_mesma_resposta()
    {
        var medico = await MedicoAprovadoAsync();

        var inexistente = await ambiente.EntrarAsync(EmailNovo(), "qualquersenha1");
        var senhaErrada = await ambiente.EntrarAsync(medico.Email, "senhaerrada123");

        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, senhaErrada.StatusCode);
        Assert.Equal(await TituloAsync(inexistente), await TituloAsync(senhaErrada));
    }

    [Fact]
    public async Task Cinco_falhas_seguidas_bloqueiam_a_conta()
    {
        var medico = await MedicoAprovadoAsync();

        for (var tentativa = 1; tentativa <= 4; tentativa++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await ambiente.EntrarAsync(medico.Email, "senhaerrada123")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Locked, (await ambiente.EntrarAsync(medico.Email, "senhaerrada123")).StatusCode);
        // Bloqueada, nem a senha certa entra.
        Assert.Equal(HttpStatusCode.Locked, (await ambiente.EntrarAsync(medico.Email, SenhaMedico)).StatusCode);
    }

    [Fact]
    public async Task Email_ou_crm_repetidos_retornam_409()
    {
        var email = EmailNovo();
        var crm = CrmNovo();
        var cliente = ambiente.Cliente();
        Assert.Equal(HttpStatusCode.Created,
            (await cliente.PostAsJsonAsync("/api/medicos/solicitacoes", Solicitacao(email, crm))).StatusCode);

        var mesmoEmail = await cliente.PostAsJsonAsync("/api/medicos/solicitacoes", Solicitacao(email, CrmNovo()));
        var mesmoCrm = await cliente.PostAsJsonAsync("/api/medicos/solicitacoes", Solicitacao(EmailNovo(), crm));

        Assert.Equal(HttpStatusCode.Conflict, mesmoEmail.StatusCode);
        Assert.Equal("Já existe cadastro com este e-mail.", await TituloAsync(mesmoEmail));
        Assert.Equal(HttpStatusCode.Conflict, mesmoCrm.StatusCode);
    }

    [Fact]
    public async Task Email_repetido_nao_deixa_medico_orfao()
    {
        var email = EmailNovo();
        var cliente = ambiente.Cliente();
        await cliente.PostAsJsonAsync("/api/medicos/solicitacoes", Solicitacao(email, CrmNovo()));
        var crmDaSegunda = CrmNovo();

        await cliente.PostAsJsonAsync("/api/medicos/solicitacoes", Solicitacao(email, crmDaSegunda));

        var existe = await ambiente.NoEscopoAsync(sp =>
            sp.GetRequiredService<AppDbContext>().Medicos.AnyAsync(m => m.Crm == crmDaSegunda));
        Assert.False(existe);
    }

    [Fact]
    public async Task Senha_fora_da_politica_retorna_422_em_portugues()
    {
        var resposta = await ambiente.Cliente().PostAsJsonAsync(
            "/api/medicos/solicitacoes", Solicitacao(EmailNovo(), CrmNovo(), senha: "curta1"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains("pelo menos 10 caracteres", await TituloAsync(resposta), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("12A45", "PI")]
    [InlineData("12345", "XX")]
    public async Task Crm_ou_uf_invalidos_retornam_422(string crm, string uf)
    {
        var solicitacao = new
        {
            nome = "Médica Sintética", email = EmailNovo(), senha = SenhaMedico, crm, ufCrm = uf, especialidade = "Pediatria",
        };

        var resposta = await ambiente.Cliente().PostAsJsonAsync("/api/medicos/solicitacoes", solicitacao);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    [Fact]
    public async Task Campos_ausentes_retornam_400_com_erros_por_campo()
    {
        var resposta = await ambiente.Cliente().PostAsJsonAsync("/api/medicos/solicitacoes", new { email = "nao-e-email" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json);
        Assert.Equal("Dados inválidos", corpo.GetProperty("title").GetString());
        var erros = corpo.GetProperty("errors");
        Assert.True(erros.TryGetProperty("Email", out _));
        Assert.True(erros.TryGetProperty("Crm", out _));
    }

    [Fact]
    public async Task Rotas_de_admin_exigem_login_e_papel_admin()
    {
        var medico = await MedicoAprovadoAsync();
        var tokenMedico = await ambiente.TokenAsync(medico.Email, SenhaMedico);

        Assert.Equal(HttpStatusCode.Unauthorized, (await ambiente.Cliente().GetAsync("/api/medicos/solicitacoes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ambiente.Cliente(tokenMedico).GetAsync("/api/medicos/solicitacoes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ambiente.Cliente(tokenMedico)
            .PostAsJsonAsync($"/api/medicos/{medico.Id}/aprovacao", new { vinculoProtocolo = "x" })).StatusCode);
    }

    [Fact]
    public async Task Token_adulterado_e_recusado()
    {
        var medico = await MedicoAprovadoAsync();
        var token = await ambiente.TokenAsync(medico.Email, SenhaMedico);
        var partes = token.Split('.');
        var adulterado = $"{partes[0]}.{partes[1]}.{new string('A', partes[2].Length)}";

        var resposta = await ambiente.Cliente(adulterado).GetAsync("/api/medicos/solicitacoes");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Inativar_a_conta_invalida_o_token_na_hora()
    {
        var medico = await MedicoAprovadoAsync();
        var token = await ambiente.TokenAsync(medico.Email, SenhaMedico);
        Assert.Equal(HttpStatusCode.Forbidden, (await ambiente.Cliente(token).GetAsync("/api/medicos/solicitacoes")).StatusCode);

        await ambiente.NoEscopoAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<AppDbContext>();
            var conta = await contexto.Users.SingleAsync(u => u.Id == medico.Id);
            conta.AlterarStatus(StatusCadastro.Inativo);
            return await contexto.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await ambiente.Cliente(token).GetAsync("/api/medicos/solicitacoes")).StatusCode);
    }

    [Fact]
    public async Task Aprovacao_valida_vinculo_existencia_e_repeticao()
    {
        var admin = await AdminAsync();
        var medico = await SolicitarAsync();

        var semVinculo = await admin.PostAsJsonAsync($"/api/medicos/{medico.Id}/aprovacao", new { vinculoProtocolo = "" });
        var inexistente = await admin.PostAsJsonAsync($"/api/medicos/{Guid.NewGuid()}/aprovacao", new { vinculoProtocolo = "Equipe" });
        await admin.PostAsJsonAsync($"/api/medicos/{medico.Id}/aprovacao", new { vinculoProtocolo = "Equipe" });
        var repetida = await admin.PostAsJsonAsync($"/api/medicos/{medico.Id}/aprovacao", new { vinculoProtocolo = "Equipe" });

        Assert.Equal(HttpStatusCode.BadRequest, semVinculo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, repetida.StatusCode);
        Assert.Equal("Este cadastro já foi analisado.", await TituloAsync(repetida));
    }

    [Fact]
    public async Task Auditoria_registra_logins_e_decisoes()
    {
        var medico = await MedicoAprovadoAsync();
        await ambiente.EntrarAsync(medico.Email, "senhaerrada123");
        await ambiente.EntrarAsync(medico.Email, SenhaMedico);

        var acoes = await ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().Auditoria
            .Where(a => a.UsuarioId == medico.Id || a.EntidadeId == medico.Id)
            .Select(a => a.Acao)
            .ToListAsync());

        Assert.Contains("MEDICO_SOLICITOU_CADASTRO", acoes);
        Assert.Contains("MEDICO_APROVADO", acoes);
        Assert.Contains("LOGIN_FALHA", acoes);
        Assert.Contains("LOGIN_OK", acoes);
    }

    [Fact]
    public async Task Admin_inicial_e_criado_uma_unica_vez()
    {
        await ambiente.NoEscopoAsync(async sp =>
        {
            await ambiente.Fabrica.Services.CriarAdminInicialAsync(
                sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>());
            return 0;
        });

        var admins = await ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().Users
            .CountAsync(u => u.Papel == PapelUsuario.Admin));

        Assert.Equal(1, admins);
    }
}
