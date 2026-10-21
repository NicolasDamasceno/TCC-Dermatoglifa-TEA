using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dermatoglifia.Api.Testes;

/// <summary>UC04 e UC05 de ponta a ponta, com dados sintéticos.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class PacientesTestes(AmbienteApi ambiente)
{
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);

    private static object Crianca(string nome = "Criança Sintética") => new
    {
        nome,
        dataNascimento = "2022-03-14",
        sexo = "MASCULINO",
        responsavel = new { nome = "Responsável Sintético", parentesco = "MAE", contato = "(86) 90000-0000" },
    };

    private static object Tcle(bool autorizaAnalise = true, bool cienteUsoIA = true, bool autorizaUsoTreino = false, DateOnly? assinadoEm = null)
        => new
        {
            tipo = "TCLE",
            versaoTermo = "CEP-v1.0",
            assinadoEm = (assinadoEm ?? Hoje.AddDays(-1)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            autorizaAnalise,
            cienteUsoIA,
            autorizaUsoTreino,
        };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage resposta)
        => await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json);

    private static async Task<Guid> CadastrarAsync(HttpClient medico, object? paciente = null)
    {
        var resposta = await medico.PostAsJsonAsync("/api/pacientes", paciente ?? Crianca());
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await JsonAsync(resposta)).GetProperty("id").GetGuid();
    }

    private static async Task<bool> PodeColetarAsync(HttpClient medico, Guid pacienteId)
        => (await medico.GetFromJsonAsync<JsonElement>($"/api/pacientes/{pacienteId}", AmbienteApi.Json))
            .GetProperty("paciente").GetProperty("podeColetar").GetBoolean();

    [Fact]
    public async Task Cadastro_e_tcle_completo_liberam_a_coleta()
    {
        var medico = await ambiente.MedicoAprovadoAsync();

        var resposta = await medico.Cliente.PostAsJsonAsync("/api/pacientes", Crianca());
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criado = await JsonAsync(resposta);
        var id = criado.GetProperty("id").GetGuid();
        Assert.False(criado.GetProperty("podeColetar").GetBoolean());
        Assert.EndsWith($"/api/pacientes/{id}", resposta.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        var consentimento = await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{id}/consentimentos", Tcle());
        Assert.Equal(HttpStatusCode.Created, consentimento.StatusCode);
        Assert.True((await JsonAsync(consentimento)).GetProperty("podeColetar").GetBoolean());

        var detalhe = await medico.Cliente.GetFromJsonAsync<JsonElement>($"/api/pacientes/{id}", AmbienteApi.Json);
        var paciente = detalhe.GetProperty("paciente");
        Assert.Equal("Criança Sintética", paciente.GetProperty("nome").GetString());
        Assert.Equal("SUSPEITA", paciente.GetProperty("situacao").GetString());
        Assert.Equal(4, paciente.GetProperty("idadeAnos").GetInt32());
        Assert.Equal("MAE", detalhe.GetProperty("responsavel").GetProperty("parentesco").GetString());
        var termo = Assert.Single(detalhe.GetProperty("consentimentos").EnumerateArray());
        Assert.Equal("TCLE", termo.GetProperty("tipo").GetString());
        Assert.True(termo.GetProperty("vigente").GetBoolean());
    }

    [Fact]
    public async Task Identificacao_fica_separada_do_paciente_no_banco()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        var id = await CadastrarAsync(medico.Cliente, Crianca("Nome Para Pseudonimizar"));

        var (colunasPaciente, nomeGuardado) = await ambiente.NoEscopoAsync(async sp =>
        {
            var contexto = sp.GetRequiredService<AppDbContext>();
            var colunas = await contexto.Database.SqlQuery<string>(
                $"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'paciente'")
                .ToListAsync();
            var nome = await contexto.Database.SqlQuery<string>(
                $"SELECT nome AS \"Value\" FROM identificacao.identificacao_paciente WHERE paciente_id = {id}")
                .SingleAsync();
            return (colunas, nome);
        });

        Assert.DoesNotContain("nome", colunasPaciente);
        Assert.DoesNotContain("data_nascimento", colunasPaciente);
        Assert.Equal("Nome Para Pseudonimizar", nomeGuardado);
    }

    [Fact]
    public async Task Menor_sem_responsavel_e_recusado()
    {
        var medico = await ambiente.MedicoAprovadoAsync();

        var resposta = await medico.Cliente.PostAsJsonAsync("/api/pacientes",
            new { nome = "Criança Sintética", dataNascimento = "2022-03-14", sexo = "FEMININO" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains("responsável legal", (await JsonAsync(resposta)).GetProperty("title").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adulto_dispensa_responsavel()
    {
        var medico = await ambiente.MedicoAprovadoAsync();

        var resposta = await medico.Cliente.PostAsJsonAsync("/api/pacientes",
            new { nome = "Adulto Sintético", dataNascimento = "1995-07-01", sexo = "MASCULINO" });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task Dados_invalidos_sao_recusados()
    {
        var medico = await ambiente.MedicoAprovadoAsync();

        var futuro = await medico.Cliente.PostAsJsonAsync("/api/pacientes",
            new { nome = "Sintético", dataNascimento = Hoje.AddYears(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), sexo = "MASCULINO" });
        var semCampos = await medico.Cliente.PostAsJsonAsync("/api/pacientes", new { nome = "Sintético" });
        var sexoInvalido = await medico.Cliente.PostAsJsonAsync("/api/pacientes",
            new { nome = "Sintético", dataNascimento = "1990-01-01", sexo = "OUTRO_VALOR" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, futuro.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, semCampos.StatusCode);
        var erros = (await JsonAsync(semCampos)).GetProperty("errors");
        Assert.True(erros.TryGetProperty("DataNascimento", out _));
        Assert.True(erros.TryGetProperty("Sexo", out _));
        Assert.Equal(HttpStatusCode.BadRequest, sexoInvalido.StatusCode);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Tcle_sem_autorizacao_ou_sem_ciencia_da_IA_nao_libera(bool autorizaAnalise, bool cienteUsoIA)
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        var id = await CadastrarAsync(medico.Cliente);

        var resposta = await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{id}/consentimentos", Tcle(autorizaAnalise, cienteUsoIA));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.False((await JsonAsync(resposta)).GetProperty("podeColetar").GetBoolean());
    }

    [Fact]
    public async Task Tale_sozinho_nao_libera_e_assinatura_futura_e_recusada()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        var id = await CadastrarAsync(medico.Cliente);

        var tale = await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{id}/consentimentos", new
        {
            tipo = "TALE", versaoTermo = "CEP-v1.0", assinadoEm = Hoje.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            autorizaAnalise = true, cienteUsoIA = true, autorizaUsoTreino = false,
        });
        var futuro = await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{id}/consentimentos", Tcle(assinadoEm: Hoje.AddDays(5)));

        Assert.False((await JsonAsync(tale)).GetProperty("podeColetar").GetBoolean());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, futuro.StatusCode);
    }

    [Fact]
    public async Task Revogar_bloqueia_a_coleta_e_so_vale_uma_vez()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        var id = await CadastrarAsync(medico.Cliente);
        var tcle = await JsonAsync(await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{id}/consentimentos", Tcle()));
        var consentimentoId = tcle.GetProperty("consentimentoId").GetGuid();
        Assert.True(await PodeColetarAsync(medico.Cliente, id));

        var revogacao = await medico.Cliente.PostAsync($"/api/pacientes/{id}/consentimentos/{consentimentoId}/revogacao", null);
        var repetida = await medico.Cliente.PostAsync($"/api/pacientes/{id}/consentimentos/{consentimentoId}/revogacao", null);
        var inexistente = await medico.Cliente.PostAsync($"/api/pacientes/{id}/consentimentos/{Guid.NewGuid()}/revogacao", null);

        Assert.Equal(HttpStatusCode.OK, revogacao.StatusCode);
        Assert.False((await JsonAsync(revogacao)).GetProperty("podeColetar").GetBoolean());
        Assert.False(await PodeColetarAsync(medico.Cliente, id));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, repetida.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inexistente.StatusCode);
    }

    [Fact]
    public async Task Medico_nao_enxerga_paciente_de_outro_medico()
    {
        var dono = await ambiente.MedicoAprovadoAsync();
        var outro = await ambiente.MedicoAprovadoAsync();
        var id = await CadastrarAsync(dono.Cliente);

        var detalhe = await outro.Cliente.GetAsync($"/api/pacientes/{id}");
        var consentimento = await outro.Cliente.PostAsJsonAsync($"/api/pacientes/{id}/consentimentos", Tcle());
        var lista = await outro.Cliente.GetFromJsonAsync<JsonElement>("/api/pacientes", AmbienteApi.Json);

        Assert.Equal(HttpStatusCode.NotFound, detalhe.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, consentimento.StatusCode);
        Assert.DoesNotContain(lista.EnumerateArray(), p => p.GetProperty("id").GetGuid() == id);
        Assert.False(await PodeColetarAsync(dono.Cliente, id));
    }

    [Fact]
    public async Task Admin_e_anonimo_nao_acessam_pacientes()
    {
        var admin = ambiente.Cliente(await ambiente.TokenAsync(AmbienteApi.EmailAdmin, AmbienteApi.SenhaAdmin));

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/pacientes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/pacientes", Crianca())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ambiente.Cliente().GetAsync("/api/pacientes")).StatusCode);
    }

    [Fact]
    public async Task Lista_traz_so_os_proprios_pacientes_e_filtra_por_nome()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        await CadastrarAsync(medico.Cliente, Crianca("Ana Sintética"));
        await CadastrarAsync(medico.Cliente, Crianca("Bruno Sintético"));

        var todos = await medico.Cliente.GetFromJsonAsync<JsonElement>("/api/pacientes", AmbienteApi.Json);
        var filtrados = await medico.Cliente.GetFromJsonAsync<JsonElement>("/api/pacientes?busca=ana", AmbienteApi.Json);

        Assert.Equal(2, todos.GetArrayLength());
        Assert.Equal("Ana Sintética", Assert.Single(filtrados.EnumerateArray()).GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Auditoria_registra_cadastro_leitura_e_consentimento()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        var id = await CadastrarAsync(medico.Cliente);
        await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{id}/consentimentos", Tcle());
        await medico.Cliente.GetAsync($"/api/pacientes/{id}");

        var registros = await ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().Auditoria
            .Where(a => a.EntidadeId == id)
            .Select(a => new { a.Acao, a.UsuarioId, a.Detalhes })
            .ToListAsync());

        Assert.Contains(registros, r => r.Acao == "PACIENTE_CRIADO");
        Assert.Contains(registros, r => r.Acao == "PACIENTE_VISUALIZADO");
        Assert.Contains(registros, r => r.Acao == "CONSENTIMENTO_REGISTRADO" && r.Detalhes!.Contains("TCLE", StringComparison.Ordinal));
        Assert.All(registros, r => Assert.Equal(medico.Id, r.UsuarioId));
        // Nenhum registro de auditoria carrega o nome do paciente.
        Assert.DoesNotContain(registros, r => r.Detalhes != null && r.Detalhes.Contains("Sintética", StringComparison.Ordinal));
    }
}
