using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dermatoglifia.Dominio.ObjetosDeValor;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dermatoglifia.Api.Testes;

/// <summary>Hierarquia de acesso (ADR-0010) e UC12 de ponta a ponta, com dados sintéticos.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class TitularTestes : IDisposable
{
    private const string NomePaciente = "Titular Sintética";
    private readonly AmbienteApi _ambiente;

    public TitularTestes(AmbienteApi ambiente)
    {
        _ambiente = ambiente;
        _ambiente.Ia.Reiniciar();
    }

    public void Dispose() => _ambiente.Ia.Reiniciar();

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage resposta)
        => await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json);

    private async Task<HttpClient> AdminAsync()
        => _ambiente.Cliente(await _ambiente.TokenAsync(AmbienteApi.EmailAdmin, AmbienteApi.SenhaAdmin));

    /// <summary>Médico + paciente com TCLE + coleta completa e analisada, com observação do médico.</summary>
    private async Task<(MedicoDeTeste Medico, Guid PacienteId, Guid AnaliseId)> PacienteAnalisadoAsync()
    {
        var medico = await _ambiente.MedicoAprovadoAsync();
        var pacienteId = (await JsonAsync(await medico.Cliente.PostAsJsonAsync("/api/pacientes", new
        {
            nome = NomePaciente, dataNascimento = "2022-03-14", sexo = "FEMININO",
            responsavel = new { nome = "Responsável Sintético", parentesco = "MAE", contato = "(00) 90000-0000" },
        }))).GetProperty("id").GetGuid();
        (await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{pacienteId}/consentimentos", new
        {
            tipo = "TCLE", versaoTermo = "CEP-v1.0",
            assinadoEm = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            autorizaAnalise = true, cienteUsoIA = true, autorizaUsoTreino = false,
        })).EnsureSuccessStatusCode();
        var sessaoId = (await JsonAsync(await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{pacienteId}/sessoes",
            new { metodo = "LEITOR_OPTICO", tipoImpressao = "ROLADA" }))).GetProperty("id").GetGuid();
        foreach (var posicao in PosicaoDedo.OrdemColeta)
        {
            (await medico.Cliente.PutAsync(
                $"/api/sessoes/{sessaoId}/digitais/{IaFalsa.Texto(posicao.Mao)}/{IaFalsa.Texto(posicao.Dedo)}",
                ImagensSinteticas.Formulario(ImagensSinteticas.Gerar()))).EnsureSuccessStatusCode();
        }

        var conclusao = await JsonAsync(await medico.Cliente.PostAsync($"/api/sessoes/{sessaoId}/conclusao", null));
        var analiseId = conclusao.GetProperty("analiseId").GetGuid();
        (await medico.Cliente.PatchAsJsonAsync($"/api/analises/{analiseId}/observacao",
            new { observacao = "Observação sintética com texto livre." })).EnsureSuccessStatusCode();
        return (medico, pacienteId, analiseId);
    }

    private Task<List<string>> CaminhosAsync(Guid pacienteId) => _ambiente.NoEscopoAsync(sp =>
    {
        var contexto = sp.GetRequiredService<AppDbContext>();
        return contexto.Digitais
            .Where(d => contexto.SessoesColeta.Any(s => s.Id == d.SessaoColetaId && s.PacienteId == pacienteId))
            .Where(d => d.CaminhoArquivo != null)
            .Select(d => d.CaminhoArquivo!)
            .ToListAsync();
    });

    [Fact]
    public async Task Admin_le_pacientes_de_todos_os_medicos_e_a_leitura_fica_auditada()
    {
        var (medico, pacienteId, analiseId) = await PacienteAnalisadoAsync();
        var admin = await AdminAsync();

        var lista = await admin.GetFromJsonAsync<JsonElement>("/api/pacientes", AmbienteApi.Json);
        Assert.Contains(lista.EnumerateArray(), p => p.GetProperty("id").GetGuid() == pacienteId);
        var detalhe = await admin.GetFromJsonAsync<JsonElement>($"/api/pacientes/{pacienteId}", AmbienteApi.Json);
        Assert.Equal(NomePaciente, detalhe.GetProperty("paciente").GetProperty("nome").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/analises/{analiseId}")).StatusCode);

        var leituras = await _ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().Auditoria
            .Where(a => a.EntidadeId == pacienteId && a.Acao == "PACIENTE_VISUALIZADO")
            .Select(a => a.UsuarioId).ToListAsync());
        Assert.Contains(leituras, u => u != medico.Id); // a leitura do administrador
    }

    [Fact]
    public async Task Admin_nao_faz_acoes_clinicas_e_medico_nao_atende_pedidos_do_titular()
    {
        var (medico, pacienteId, _) = await PacienteAnalisadoAsync();
        var admin = await AdminAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/pacientes/{pacienteId}/consentimentos", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PatchAsJsonAsync($"/api/pacientes/{pacienteId}/situacao-diagnostica", new { situacao = "CONTROLE" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/pacientes/{pacienteId}/sessoes", new { metodo = "LEITOR_OPTICO", tipoImpressao = "ROLADA" })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await medico.Cliente.GetAsync($"/api/pacientes/{pacienteId}/exportacao")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await medico.Cliente.PutAsJsonAsync($"/api/pacientes/{pacienteId}/identificacao", new { nome = "x", dataNascimento = "2022-03-14" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{pacienteId}/eliminacao", new { confirmacao = "ELIMINAR" })).StatusCode);
    }

    [Fact]
    public async Task Exportacao_traz_os_dados_e_as_imagens_decifradas()
    {
        var (_, pacienteId, _) = await PacienteAnalisadoAsync();
        var admin = await AdminAsync();

        var resposta = await admin.GetAsync($"/api/pacientes/{pacienteId}/exportacao");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/zip", resposta.Content.Headers.ContentType?.MediaType);
        using var zip = new ZipArchive(await resposta.Content.ReadAsStreamAsync());
        var imagens = zip.Entries.Where(e => e.FullName.StartsWith("digitais/", StringComparison.Ordinal)).ToList();
        Assert.Equal(10, imagens.Count);
        await using (var png = await imagens[0].OpenAsync())
        {
            var cabecalho = new byte[8];
            await png.ReadExactlyAsync(cabecalho);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, cabecalho); // PNG, não o arquivo cifrado
        }

        await using var json = await zip.GetEntry("dados.json")!.OpenAsync();
        var dados = await JsonDocument.ParseAsync(json);
        Assert.Equal(NomePaciente, dados.RootElement.GetProperty("paciente").GetProperty("nome").GetString());
        var coleta = Assert.Single(dados.RootElement.GetProperty("coletas").EnumerateArray());
        Assert.Equal(10, coleta.GetProperty("analise").GetProperty("resultados").GetArrayLength());

        var registro = await _ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().Auditoria
            .AnyAsync(a => a.EntidadeId == pacienteId && a.Acao == "PACIENTE_EXPORTADO"));
        Assert.True(registro);
    }

    [Fact]
    public async Task Correcao_da_identificacao_segue_as_regras_do_cadastro()
    {
        var (medico, pacienteId, _) = await PacienteAnalisadoAsync();
        var admin = await AdminAsync();
        var rota = $"/api/pacientes/{pacienteId}/identificacao";

        // Menor de idade sem responsável é recusado, como no cadastro.
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await admin.PutAsJsonAsync(rota, new { nome = "Nome Corrigido", dataNascimento = "2022-03-14" })).StatusCode);

        var resposta = await admin.PutAsJsonAsync(rota, new
        {
            nome = "Nome Corrigido Sintético", dataNascimento = "2022-04-14",
            responsavel = new { nome = "Responsável Corrigido", parentesco = "PAI", contato = "(00) 91111-1111" },
        });

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var detalhe = await medico.Cliente.GetFromJsonAsync<JsonElement>($"/api/pacientes/{pacienteId}", AmbienteApi.Json);
        Assert.Equal("Nome Corrigido Sintético", detalhe.GetProperty("paciente").GetProperty("nome").GetString());
        Assert.Equal("PAI", detalhe.GetProperty("responsavel").GetProperty("parentesco").GetString());
        var log = await _ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().Auditoria
            .Where(a => a.EntidadeId == pacienteId && a.Acao == "IDENTIFICACAO_CORRIGIDA").Select(a => a.Detalhes).SingleAsync());
        Assert.Null(log); // nenhum valor pessoal no log
    }

    [Fact]
    public async Task Eliminacao_apaga_identificacao_imagens_e_textos_livres_e_mantem_os_indicadores()
    {
        var (medico, pacienteId, analiseId) = await PacienteAnalisadoAsync();
        var admin = await AdminAsync();
        var caminhos = await CaminhosAsync(pacienteId);
        Assert.Equal(10, caminhos.Count);
        Assert.All(caminhos, c => Assert.True(File.Exists(Path.Combine(_ambiente.DiretorioImagens, c))));
        var rota = $"/api/pacientes/{pacienteId}/eliminacao";

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(rota, new { confirmacao = "sim" })).StatusCode);

        var resposta = await admin.PostAsJsonAsync(rota, new { confirmacao = "ELIMINAR" });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(10, (await JsonAsync(resposta)).GetProperty("imagensApagadas").GetInt32());
        Assert.All(caminhos, c => Assert.False(File.Exists(Path.Combine(_ambiente.DiretorioImagens, c))));
        Assert.Empty(await CaminhosAsync(pacienteId));

        var detalhe = await medico.Cliente.GetFromJsonAsync<JsonElement>($"/api/pacientes/{pacienteId}", AmbienteApi.Json);
        Assert.True(detalhe.GetProperty("paciente").GetProperty("anonimizado").GetBoolean());
        Assert.Equal(JsonValueKind.Null, detalhe.GetProperty("paciente").GetProperty("nome").ValueKind);
        var analise = await medico.Cliente.GetFromJsonAsync<JsonElement>($"/api/analises/{analiseId}", AmbienteApi.Json);
        Assert.Equal("CONCLUIDA", analise.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, analise.GetProperty("observacaoMedico").ValueKind);
        Assert.Equal(10, analise.GetProperty("indicadores").GetProperty("qtdVerticilos").GetInt32());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(rota, new { confirmacao = "ELIMINAR" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync($"/api/pacientes/{pacienteId}/exportacao")).StatusCode);
    }

    [Fact]
    public async Task Admin_revoga_termo_a_pedido_do_titular()
    {
        var (_, pacienteId, _) = await PacienteAnalisadoAsync();
        var admin = await AdminAsync();
        var detalhe = await admin.GetFromJsonAsync<JsonElement>($"/api/pacientes/{pacienteId}", AmbienteApi.Json);
        var termo = detalhe.GetProperty("consentimentos")[0].GetProperty("id").GetGuid();

        var resposta = await admin.PostAsync($"/api/pacientes/{pacienteId}/consentimentos/{termo}/revogacao", null);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.False((await JsonAsync(resposta)).GetProperty("podeColetar").GetBoolean());
    }
}
