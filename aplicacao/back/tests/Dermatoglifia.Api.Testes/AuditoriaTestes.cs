using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Dermatoglifia.Api.Testes;

/// <summary>UC11 de ponta a ponta, com dados sintéticos.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AuditoriaTestes(AmbienteApi ambiente)
{
    private static readonly object Crianca = new
    {
        nome = "Criança Sintética Auditada",
        dataNascimento = "2022-03-14",
        sexo = "FEMININO",
        responsavel = new { nome = "Responsável Sintético", parentesco = "MAE", contato = "(00) 0000-0000" },
    };

    private async Task<HttpClient> AdminAsync()
        => ambiente.Cliente(await ambiente.TokenAsync(AmbienteApi.EmailAdmin, AmbienteApi.SenhaAdmin));

    private static async Task<JsonElement> ConsultarAsync(HttpClient admin, string consulta)
        => await admin.GetFromJsonAsync<JsonElement>($"/api/auditoria?{consulta}", AmbienteApi.Json);

    private async Task<(MedicoDeTeste Medico, Guid PacienteId, Guid SessaoId)> PacienteComSessaoAsync()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        var criado = await medico.Cliente.PostAsJsonAsync("/api/pacientes", Crianca);
        var pacienteId = (await criado.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json)).GetProperty("id").GetGuid();
        (await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{pacienteId}/consentimentos", new
        {
            tipo = "TCLE", versaoTermo = "CEP-v1.0",
            assinadoEm = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            autorizaAnalise = true, cienteUsoIA = true, autorizaUsoTreino = false,
        })).EnsureSuccessStatusCode();
        var sessao = await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{pacienteId}/sessoes",
            new { metodo = "LEITOR_OPTICO", tipoImpressao = "ROLADA", dispositivo = "Leitor sintético" });
        var sessaoId = (await sessao.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json)).GetProperty("id").GetGuid();
        return (medico, pacienteId, sessaoId);
    }

    [Fact]
    public async Task Filtro_por_paciente_traz_tambem_as_sessoes_dele()
    {
        var (medico, pacienteId, sessaoId) = await PacienteComSessaoAsync();
        await medico.Cliente.GetAsync($"/api/pacientes/{pacienteId}");
        var admin = await AdminAsync();

        var pagina = await ConsultarAsync(admin, $"pacienteId={pacienteId}");

        var itens = pagina.GetProperty("itens").EnumerateArray().ToList();
        var acoes = itens.Select(i => i.GetProperty("acao").GetString()).ToList();
        Assert.Equal(["PACIENTE_VISUALIZADO", "SESSAO_INICIADA", "CONSENTIMENTO_REGISTRADO", "PACIENTE_CRIADO"], acoes);
        Assert.Equal(4, pagina.GetProperty("total").GetInt32());
        var sessao = itens.Single(i => i.GetProperty("acao").GetString() == "SESSAO_INICIADA");
        Assert.Equal(sessaoId, sessao.GetProperty("entidadeId").GetGuid());
        Assert.All(itens, i =>
        {
            Assert.Equal(medico.Id, i.GetProperty("usuarioId").GetGuid());
            Assert.Equal(medico.Email, i.GetProperty("usuario").GetProperty("email").GetString());
        });
        // O administrador não vê o nome do paciente em nenhum lugar da trilha.
        Assert.DoesNotContain("Criança Sintética Auditada", pagina.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Filtros_por_usuario_acao_e_periodo_e_paginacao()
    {
        var (medico, _, _) = await PacienteComSessaoAsync();
        var admin = await AdminAsync();

        var doMedico = await ConsultarAsync(admin, $"usuarioId={medico.Id}&acao=PACIENTE_CRIADO");
        var item = Assert.Single(doMedico.GetProperty("itens").EnumerateArray());
        Assert.Equal("paciente", item.GetProperty("entidade").GetString());

        var desde = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"));
        var ate = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"));
        var pagina1 = await ConsultarAsync(admin, $"usuarioId={medico.Id}&de={desde}&ate={ate}&tamanho=2");
        Assert.True(pagina1.GetProperty("total").GetInt32() >= 4);
        Assert.Equal(2, pagina1.GetProperty("itens").GetArrayLength());
        var pagina2 = await ConsultarAsync(admin, $"usuarioId={medico.Id}&de={desde}&ate={ate}&tamanho=2&pagina=2");
        Assert.NotEqual(
            pagina1.GetProperty("itens")[0].GetProperty("id").GetInt64(),
            pagina2.GetProperty("itens")[0].GetProperty("id").GetInt64());

        var passado = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddYears(-5).ToString("O"));
        var vazio = await ConsultarAsync(admin, $"usuarioId={medico.Id}&ate={passado}");
        Assert.Equal(0, vazio.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Consulta_fica_registrada_e_filtros_listam_acoes_e_usuarios()
    {
        var admin = await AdminAsync();
        await ConsultarAsync(admin, "acao=LOGIN_OK");

        var filtros = await admin.GetFromJsonAsync<JsonElement>("/api/auditoria/filtros", AmbienteApi.Json);
        var acoes = filtros.GetProperty("acoes").EnumerateArray().Select(a => a.GetString()).ToList();
        Assert.Contains("AUDITORIA_CONSULTADA", acoes);
        Assert.Contains("LOGIN_OK", acoes);
        Assert.Contains(filtros.GetProperty("usuarios").EnumerateArray(),
            u => u.GetProperty("email").GetString() == AmbienteApi.EmailAdmin && u.GetProperty("papel").GetString() == "ADMIN");
    }

    [Fact]
    public async Task Periodo_invertido_e_pagina_grande_demais_sao_recusados()
    {
        var admin = await AdminAsync();
        var agora = DateTimeOffset.UtcNow;
        var de = Uri.EscapeDataString(agora.ToString("O"));
        var ate = Uri.EscapeDataString(agora.AddDays(-1).ToString("O"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync($"/api/auditoria?de={de}&ate={ate}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/auditoria?tamanho=500")).StatusCode);
    }

    [Fact]
    public async Task So_o_administrador_acessa()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await medico.Cliente.GetAsync("/api/auditoria")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await medico.Cliente.GetAsync("/api/auditoria/filtros")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ambiente.Cliente().GetAsync("/api/auditoria")).StatusCode);
    }
}
