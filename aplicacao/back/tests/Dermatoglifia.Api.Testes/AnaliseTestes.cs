using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.ObjetosDeValor;
using Dermatoglifia.Infraestrutura.Persistencia;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dermatoglifia.Api.Testes;

/// <summary>UC07 e UC08 de ponta a ponta, com a IA falsa no lugar do serviço Python.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class AnaliseTestes : IDisposable
{
    private const string NomePaciente = "Criança Sintética Análise";
    private readonly AmbienteApi _ambiente;

    public AnaliseTestes(AmbienteApi ambiente)
    {
        _ambiente = ambiente;
        _ambiente.Ia.Reiniciar();
    }

    public void Dispose() => _ambiente.Ia.Reiniciar();

    private static readonly PosicaoDedo[] Ordem = [.. PosicaoDedo.OrdemColeta];

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage resposta)
        => await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json);

    /// <summary>Médico + paciente com TCLE + sessão com as 10 digitais enviadas (sem concluir).</summary>
    private async Task<(MedicoDeTeste Medico, Guid SessaoId)> SessaoProntaAsync(string sexo = "FEMININO")
    {
        var medico = await _ambiente.MedicoAprovadoAsync();
        var paciente = await JsonAsync(await medico.Cliente.PostAsJsonAsync("/api/pacientes", new
        {
            nome = NomePaciente, dataNascimento = "2022-03-14", sexo,
            responsavel = new { nome = "Responsável Sintético", parentesco = "MAE", contato = "(86) 90000-0000" },
        }));
        var pacienteId = paciente.GetProperty("id").GetGuid();
        await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{pacienteId}/consentimentos", new
        {
            tipo = "TCLE", versaoTermo = "CEP-v1.0",
            assinadoEm = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            autorizaAnalise = true, cienteUsoIA = true, autorizaUsoTreino = false,
        });
        var sessao = await JsonAsync(await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{pacienteId}/sessoes",
            new { metodo = "LEITOR_OPTICO", tipoImpressao = "ROLADA" }));
        var sessaoId = sessao.GetProperty("id").GetGuid();

        foreach (var posicao in Ordem)
        {
            var resposta = await medico.Cliente.PutAsync(
                $"/api/sessoes/{sessaoId}/digitais/{IaFalsa.Texto(posicao.Mao)}/{IaFalsa.Texto(posicao.Dedo)}",
                ImagensSinteticas.Formulario(ImagensSinteticas.Gerar()));
            resposta.EnsureSuccessStatusCode();
        }

        return (medico, sessaoId);
    }

    private static async Task<JsonElement> ConcluirAsync(HttpClient cliente, Guid sessaoId)
    {
        var resposta = await cliente.PostAsync($"/api/sessoes/{sessaoId}/conclusao", null);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await JsonAsync(resposta);
    }

    private static async Task<JsonElement> AnaliseAsync(HttpClient cliente, Guid analiseId)
        => await cliente.GetFromJsonAsync<JsonElement>($"/api/analises/{analiseId}", AmbienteApi.Json);

    private static List<string> Alertas(JsonElement analise)
        => [.. analise.GetProperty("alertas").EnumerateArray().Select(a => a.GetProperty("codigo").GetString()!)];

    [Fact]
    public async Task Concluir_a_sessao_gera_analise_concluida_com_aviso_e_indicadores()
    {
        // Alça para a direita em todos os dedos, exceto o indicador esquerdo (arco tendido).
        _ambiente.Ia.ClassePadrao = ClasseImagem.AlcaDireita;
        _ambiente.Ia.ClassePorDedo[new PosicaoDedo(Mao.Esquerda, Dedo.Indicador)] = ClasseImagem.ArcoTendido;
        var (medico, sessaoId) = await SessaoProntaAsync();

        var sessao = await ConcluirAsync(medico.Cliente, sessaoId);

        Assert.Equal("COMPLETA", sessao.GetProperty("status").GetString());
        Assert.Equal("CONCLUIDA", sessao.GetProperty("analiseStatus").GetString());
        var analise = await AnaliseAsync(medico.Cliente, sessao.GetProperty("analiseId").GetGuid());

        Assert.Equal("CONCLUIDA", analise.GetProperty("status").GetString());
        Assert.Equal(sessaoId, analise.GetProperty("sessaoId").GetGuid());
        Assert.Equal("v1", analise.GetProperty("aviso").GetProperty("versao").GetString());
        Assert.StartsWith("Protótipo de pesquisa", analise.GetProperty("aviso").GetProperty("texto").GetString(), StringComparison.Ordinal);
        Assert.Equal(
            ["SEM_PONTO_DE_CORTE_VALIDADO", "ESCORE_NAO_VALIDADO", "MODELO_SINTETICO", "POUCOS_DADOS_FEMININOS"],
            Alertas(analise));

        var digitais = analise.GetProperty("digitais").EnumerateArray().ToList();
        Assert.Equal(10, digitais.Count);
        Assert.Equal(("DIREITA", "POLEGAR", "ALCA_ULNAR"),
            (digitais[0].GetProperty("mao").GetString(), digitais[0].GetProperty("dedo").GetString(), digitais[0].GetProperty("padrao").GetString()));
        Assert.Equal(("ESQUERDA", "INDICADOR", "ARCO_TENDIDO"),
            (digitais[6].GetProperty("mao").GetString(), digitais[6].GetProperty("dedo").GetString(), digitais[6].GetProperty("padrao").GetString()));
        Assert.Equal("ALCA_RADIAL", digitais[9].GetProperty("padrao").GetString());

        var indicadores = analise.GetProperty("indicadores");
        Assert.Equal(5, indicadores.GetProperty("qtdAlcasUlnares").GetInt32());
        Assert.Equal(4, indicadores.GetProperty("qtdAlcasRadiais").GetInt32());
        Assert.Equal(1, indicadores.GetProperty("qtdArcosTendidos").GetInt32());
        Assert.Equal(4, indicadores.GetProperty("discordanciaBilateral").GetInt32());
        Assert.Equal(5, indicadores.GetProperty("discordanciaBilateralComPolegar").GetInt32());
        Assert.Equal(JsonValueKind.Null, analise.GetProperty("escoreRisco").ValueKind);

        var modelo = analise.GetProperty("modelo");
        Assert.Equal(AmbienteApi.VersaoModeloTeste, modelo.GetProperty("versao").GetString());
        Assert.Equal("SINTETICO", modelo.GetProperty("origemDados").GetString());
        Assert.Equal(0.9m, modelo.GetProperty("metricas").GetProperty("acuraciaBalanceada").GetDecimal());
    }

    [Fact]
    public async Task Envia_so_as_dez_imagens_sexo_e_idade_sem_identificacao()
    {
        var (medico, sessaoId) = await SessaoProntaAsync();

        await ConcluirAsync(medico.Cliente, sessaoId);

        var chamada = Assert.Single(_ambiente.Ia.Chamadas);
        Assert.Equal(("POST", "/inferencia"), (chamada.Metodo, chamada.Caminho));
        Assert.Equal(Ordem.Select(p => p.NomeArquivo).Order(), chamada.Arquivos.Keys.Order());
        Assert.Equal(["idade_meses", "sexo"], chamada.Campos.Keys.Order());
        Assert.Equal("FEMININO", chamada.Campos["sexo"]);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var meses = ((hoje.Year - 2022) * 12) + hoje.Month - 3 - (hoje.Day < 14 ? 1 : 0);
        Assert.Equal(meses.ToString(CultureInfo.InvariantCulture), chamada.Campos["idade_meses"]);

        foreach (var (nome, arquivo) in chamada.Arquivos)
        {
            Assert.Equal($"{nome}.png", arquivo.NomeArquivo);
            Assert.Equal("image/png", arquivo.TipoConteudo);
            using var imagem = new MagickImage(arquivo.Conteudo);
            Assert.Equal(MagickFormat.Png, imagem.Format);
            Assert.Equal(ColorSpace.Gray, imagem.ColorSpace);
        }

        // Nada que identifique o paciente sai para o Serviço de IA.
        var texto = string.Join("|", chamada.Campos.Values.Concat(chamada.Arquivos.Values.Select(a => a.NomeArquivo)));
        Assert.DoesNotContain("Sintética", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(sessaoId.ToString(), texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ia_indisponivel_tenta_de_novo_uma_vez_e_permite_nova_tentativa()
    {
        _ambiente.Ia.Responder = (_, _) => throw new HttpRequestException("Conexão recusada");
        var (medico, sessaoId) = await SessaoProntaAsync();

        var sessao = await ConcluirAsync(medico.Cliente, sessaoId);

        Assert.Equal("ERRO", sessao.GetProperty("analiseStatus").GetString());
        Assert.Equal(2, _ambiente.Ia.Chamadas.Count); // tentativa + uma nova tentativa automática
        var analiseId = sessao.GetProperty("analiseId").GetGuid();
        var analise = await AnaliseAsync(medico.Cliente, analiseId);
        Assert.Equal("ERRO", analise.GetProperty("status").GetString());
        Assert.StartsWith("Serviço de IA indisponível", analise.GetProperty("mensagemErro").GetString(), StringComparison.Ordinal);
        Assert.Empty(analise.GetProperty("digitais").EnumerateArray());

        _ambiente.Ia.Responder = null; // serviço volta
        var novaTentativa = await medico.Cliente.PostAsync($"/api/analises/{analiseId}/nova-tentativa", null);

        Assert.Equal(HttpStatusCode.OK, novaTentativa.StatusCode);
        var depois = await JsonAsync(novaTentativa);
        Assert.Equal("CONCLUIDA", depois.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, depois.GetProperty("mensagemErro").ValueKind);
        Assert.Equal(10, depois.GetProperty("digitais").GetArrayLength());
    }

    [Fact]
    public async Task Erro_inesperado_vira_erro_reprocessavel_sem_expor_detalhes()
    {
        _ambiente.Ia.Responder = (_, _) => throw new InvalidOperationException("detalhe interno 7f3a");
        var (medico, sessaoId) = await SessaoProntaAsync();

        var conclusao = await medico.Cliente.PostAsync($"/api/sessoes/{sessaoId}/conclusao", null);

        Assert.Equal(HttpStatusCode.OK, conclusao.StatusCode);
        var sessao = await JsonAsync(conclusao);
        Assert.Equal("ERRO", sessao.GetProperty("analiseStatus").GetString());
        var analiseId = sessao.GetProperty("analiseId").GetGuid();
        var corpo = await medico.Cliente.GetStringAsync($"/api/analises/{analiseId}");
        Assert.Contains("Erro inesperado ao processar a análise", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("7f3a", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", corpo, StringComparison.Ordinal);

        // Não fica presa em PROCESSANDO: a nova tentativa vale na hora.
        _ambiente.Ia.Responder = null;
        var novaTentativa = await JsonAsync(await medico.Cliente.PostAsync($"/api/analises/{analiseId}/nova-tentativa", null));
        Assert.Equal("CONCLUIDA", novaTentativa.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Erro_http_da_ia_nao_e_repetido_automaticamente()
    {
        _ambiente.Ia.Responder = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var (medico, sessaoId) = await SessaoProntaAsync();

        var sessao = await ConcluirAsync(medico.Cliente, sessaoId);

        Assert.Equal("ERRO", sessao.GetProperty("analiseStatus").GetString());
        Assert.Single(_ambiente.Ia.Chamadas);
        var analise = await AnaliseAsync(medico.Cliente, sessao.GetProperty("analiseId").GetGuid());
        Assert.Contains("HTTP 500", analise.GetProperty("mensagemErro").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ia_lenta_estoura_o_tempo_limite()
    {
        _ambiente.Ia.Responder = async (_, cancelamento) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancelamento);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        var (medico, sessaoId) = await SessaoProntaAsync();

        var sessao = await ConcluirAsync(medico.Cliente, sessaoId);

        var analise = await AnaliseAsync(medico.Cliente, sessao.GetProperty("analiseId").GetGuid());
        Assert.Equal("ERRO", analise.GetProperty("status").GetString());
        Assert.Contains("tempo limite", analise.GetProperty("mensagemErro").GetString(), StringComparison.Ordinal);
    }

    public static TheoryData<string, string> RespostasInvalidas => new()
    {
        { "padrao_incoerente", "Resposta do modelo inconsistente" },
        { "indicadores_adulterados", "Resposta do modelo inconsistente" },
        { "dedo_faltando", "Resposta do modelo inconsistente" },
        { "versao_desconhecida", "não está registrada" },
        { "probabilidades_invalidas", "Resposta do modelo inconsistente" },
        { "campo_ausente", "formato inválido" },
        { "enum_desconhecido", "formato inválido" },
    };

    [Theory]
    [MemberData(nameof(RespostasInvalidas))]
    public async Task Resposta_invalida_da_ia_vira_erro_sem_gravar_resultados(string caso, string mensagemEsperada)
    {
        _ambiente.Ia.Responder = (chamada, _) =>
        {
            var corpo = _ambiente.Ia.RespostaConsistente(chamada);
            var digitais = (List<Dictionary<string, object?>>)corpo["digitais"]!;
            var indicadores = (Dictionary<string, int>)corpo["indicadores"]!;
            switch (caso)
            {
                case "padrao_incoerente":
                    digitais[0]["classe_imagem"] = "ALCA_DIREITA";
                    digitais[0]["padrao"] = "ALCA_RADIAL"; // mão direita: seria ulnar
                    break;
                case "indicadores_adulterados":
                    indicadores["discordancia_bilateral"] = 3;
                    break;
                case "dedo_faltando":
                    digitais.RemoveAt(9);
                    break;
                case "versao_desconhecida":
                    corpo["versao_modelo"] = "modelo_trocado_no_servidor";
                    break;
                case "probabilidades_invalidas":
                    ((Dictionary<string, decimal>)digitais[2]["probabilidades"]!)["ARCO"] = 0.7m;
                    break;
                case "campo_ausente":
                    corpo.Remove("indicadores");
                    break;
                case "enum_desconhecido":
                    digitais[1]["padrao"] = "PRESILHA_DUPLA";
                    break;
            }

            return Task.FromResult(IaFalsa.Json(corpo));
        };
        var (medico, sessaoId) = await SessaoProntaAsync();

        var sessao = await ConcluirAsync(medico.Cliente, sessaoId);

        var analiseId = sessao.GetProperty("analiseId").GetGuid();
        var analise = await AnaliseAsync(medico.Cliente, analiseId);
        Assert.Equal("ERRO", analise.GetProperty("status").GetString());
        Assert.Contains(mensagemEsperada, analise.GetProperty("mensagemErro").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, analise.GetProperty("indicadores").ValueKind);
        var resultadosGravados = await _ambiente.NoEscopoAsync(sp =>
            sp.GetRequiredService<AppDbContext>().ResultadosDigitais.CountAsync(r => r.AnaliseId == analiseId));
        Assert.Equal(0, resultadosGravados);
    }

    [Fact]
    public async Task Escore_so_aparece_com_etapa_2_validada()
    {
        _ambiente.Ia.Escore = 0.73m;
        var (medico, sessaoId) = await SessaoProntaAsync(sexo: "MASCULINO");

        var naoValidado = await AnaliseAsync(medico.Cliente, (await ConcluirAsync(medico.Cliente, sessaoId)).GetProperty("analiseId").GetGuid());
        Assert.Equal(JsonValueKind.Null, naoValidado.GetProperty("escoreRisco").ValueKind);
        Assert.Contains("ESCORE_NAO_VALIDADO", Alertas(naoValidado));
        Assert.DoesNotContain("POUCOS_DADOS_FEMININOS", Alertas(naoValidado));

        // Troca temporária do modelo ativo por um com a Etapa 2 validada.
        await TrocarModeloAtivoAsync("teste_validado_v1", escoreValidado: true);
        try
        {
            _ambiente.Ia.Versao = "teste_validado_v1";
            var (medico2, sessao2) = await SessaoProntaAsync(sexo: "MASCULINO");
            var validado = await AnaliseAsync(medico2.Cliente,
                (await ConcluirAsync(medico2.Cliente, sessao2)).GetProperty("analiseId").GetGuid());

            Assert.Equal(0.73m, validado.GetProperty("escoreRisco").GetDecimal());
            Assert.DoesNotContain("ESCORE_NAO_VALIDADO", Alertas(validado));
            Assert.True(validado.GetProperty("modelo").GetProperty("escoreValidado").GetBoolean());
        }
        finally
        {
            await TrocarModeloAtivoAsync(AmbienteApi.VersaoModeloTeste, escoreValidado: false);
        }
    }

    [Fact]
    public async Task Alerta_de_qualidade_da_ia_aparece_no_dedo_e_nos_alertas()
    {
        _ambiente.Ia.AlertaQualidadeEm.Add(new PosicaoDedo(Mao.Esquerda, Dedo.Minimo));
        var (medico, sessaoId) = await SessaoProntaAsync();

        var analise = await AnaliseAsync(medico.Cliente, (await ConcluirAsync(medico.Cliente, sessaoId)).GetProperty("analiseId").GetGuid());

        Assert.Contains("QUALIDADE_IMAGEM", Alertas(analise));
        var digitais = analise.GetProperty("digitais").EnumerateArray().ToList();
        Assert.True(digitais[9].GetProperty("alertaQualidade").GetBoolean());
        Assert.False(digitais[0].GetProperty("alertaQualidade").GetBoolean());
    }

    [Fact]
    public async Task Imagem_adulterada_no_disco_impede_a_analise_sem_chamar_a_ia()
    {
        var (medico, sessaoId) = await SessaoProntaAsync();
        var arquivo = Directory.GetFiles(Path.Combine(_ambiente.DiretorioImagens, sessaoId.ToString("N"))).First();
        var bytes = await File.ReadAllBytesAsync(arquivo);
        bytes[^1] ^= 0x01;
        await File.WriteAllBytesAsync(arquivo, bytes);

        var sessao = await ConcluirAsync(medico.Cliente, sessaoId);

        Assert.Equal("ERRO", sessao.GetProperty("analiseStatus").GetString());
        Assert.Empty(_ambiente.Ia.Chamadas);
        var analise = await AnaliseAsync(medico.Cliente, sessao.GetProperty("analiseId").GetGuid());
        Assert.Contains("não pôde ser lida", analise.GetProperty("mensagemErro").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sessao_incompleta_nao_gera_analise()
    {
        var (medico, sessaoId) = await SessaoProntaAsync();
        await medico.Cliente.PostAsJsonAsync($"/api/sessoes/{sessaoId}/digitais/ESQUERDA/ANELAR/nao-coletavel", new { motivo = "Lesão" });

        var sessao = await ConcluirAsync(medico.Cliente, sessaoId);

        Assert.Equal("INCOMPLETA", sessao.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, sessao.GetProperty("analiseId").ValueKind);
        Assert.Empty(_ambiente.Ia.Chamadas);
    }

    [Fact]
    public async Task Sessao_mostra_a_analise_vinculada()
    {
        var (medico, sessaoId) = await SessaoProntaAsync();
        var concluida = await ConcluirAsync(medico.Cliente, sessaoId);

        var sessao = await medico.Cliente.GetFromJsonAsync<JsonElement>($"/api/sessoes/{sessaoId}", AmbienteApi.Json);

        Assert.Equal(concluida.GetProperty("analiseId").GetGuid(), sessao.GetProperty("analiseId").GetGuid());
        Assert.Equal("CONCLUIDA", sessao.GetProperty("analiseStatus").GetString());
    }

    [Fact]
    public async Task Observacao_e_registro_no_prontuario()
    {
        var (medico, sessaoId) = await SessaoProntaAsync();
        var analiseId = (await ConcluirAsync(medico.Cliente, sessaoId)).GetProperty("analiseId").GetGuid();

        var observacao = await medico.Cliente.PatchAsJsonAsync($"/api/analises/{analiseId}/observacao",
            new { observacao = "Indicadores sem relação aparente com o quadro clínico." });
        var prontuario = await medico.Cliente.PostAsync($"/api/analises/{analiseId}/registro-prontuario", null);
        var repetido = await medico.Cliente.PostAsync($"/api/analises/{analiseId}/registro-prontuario", null);
        var concluidaDeNovo = await medico.Cliente.PostAsync($"/api/analises/{analiseId}/nova-tentativa", null);

        Assert.Equal(HttpStatusCode.OK, observacao.StatusCode);
        Assert.Equal("Indicadores sem relação aparente com o quadro clínico.",
            (await JsonAsync(observacao)).GetProperty("observacaoMedico").GetString());
        Assert.Equal(HttpStatusCode.OK, prontuario.StatusCode);
        Assert.NotEqual(JsonValueKind.Null, (await JsonAsync(prontuario)).GetProperty("registradoProntuarioEm").ValueKind);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, repetido.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, concluidaDeNovo.StatusCode);
    }

    [Fact]
    public async Task Analise_com_erro_nao_aceita_registro_no_prontuario()
    {
        _ambiente.Ia.Responder = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var (medico, sessaoId) = await SessaoProntaAsync();
        var analiseId = (await ConcluirAsync(medico.Cliente, sessaoId)).GetProperty("analiseId").GetGuid();

        var prontuario = await medico.Cliente.PostAsync($"/api/analises/{analiseId}/registro-prontuario", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, prontuario.StatusCode);
    }

    [Fact]
    public async Task Outro_medico_nao_acessa_e_admin_so_le_a_analise()
    {
        var (medico, sessaoId) = await SessaoProntaAsync();
        var analiseId = (await ConcluirAsync(medico.Cliente, sessaoId)).GetProperty("analiseId").GetGuid();
        var outro = await _ambiente.MedicoAprovadoAsync();
        var admin = _ambiente.Cliente(await _ambiente.TokenAsync(AmbienteApi.EmailAdmin, AmbienteApi.SenhaAdmin));

        Assert.Equal(HttpStatusCode.NotFound, (await outro.Cliente.GetAsync($"/api/analises/{analiseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Cliente.PostAsync($"/api/analises/{analiseId}/nova-tentativa", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Cliente.PatchAsJsonAsync($"/api/analises/{analiseId}/observacao", new { observacao = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Cliente.PostAsync($"/api/analises/{analiseId}/registro-prontuario", null)).StatusCode);
        // ADR-0010: o administrador lê o resultado, mas não age sobre ele.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/analises/{analiseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"/api/analises/{analiseId}/nova-tentativa", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PatchAsJsonAsync($"/api/analises/{analiseId}/observacao", new { observacao = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"/api/analises/{analiseId}/registro-prontuario", null)).StatusCode);
    }

    [Fact]
    public async Task Auditoria_registra_o_ciclo_da_analise_sem_dados_pessoais()
    {
        _ambiente.Ia.Responder = (_, _) => throw new HttpRequestException("fora do ar");
        var (medico, sessaoId) = await SessaoProntaAsync();
        var analiseId = (await ConcluirAsync(medico.Cliente, sessaoId)).GetProperty("analiseId").GetGuid();
        _ambiente.Ia.Responder = null;
        await medico.Cliente.PostAsync($"/api/analises/{analiseId}/nova-tentativa", null);
        await AnaliseAsync(medico.Cliente, analiseId);

        var registros = await _ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().Auditoria
            .Where(a => a.EntidadeId == analiseId)
            .OrderBy(a => a.Id)
            .Select(a => new { a.Acao, a.Detalhes, a.UsuarioId })
            .ToListAsync());

        Assert.Equal(
            ["ANALISE_SOLICITADA", "ANALISE_ERRO", "ANALISE_NOVA_TENTATIVA", "ANALISE_CONCLUIDA", "ANALISE_VISUALIZADA"],
            registros.Select(r => r.Acao));
        Assert.All(registros, r => Assert.Equal(medico.Id, r.UsuarioId));
        Assert.Contains("IA_INDISPONIVEL_OU_INVALIDA", registros[1].Detalhes, StringComparison.Ordinal);
        Assert.Contains(AmbienteApi.VersaoModeloTeste, registros[3].Detalhes, StringComparison.Ordinal);
        Assert.DoesNotContain(registros, r => r.Detalhes != null && r.Detalhes.Contains("Sintética", StringComparison.Ordinal));
    }

    private Task<int> TrocarModeloAtivoAsync(string versao, bool escoreValidado) => _ambiente.NoEscopoAsync(async sp =>
    {
        var contexto = sp.GetRequiredService<AppDbContext>();
        foreach (var ativo in await contexto.ModelosIA.Where(m => m.Ativo).ToListAsync())
        {
            ativo.Desativar();
        }

        await contexto.SaveChangesAsync(); // o índice único exige desativar antes de ativar outro

        var modelo = await contexto.ModelosIA.SingleOrDefaultAsync(m => m.Versao == versao);
        if (modelo is null)
        {
            modelo = ModeloIA.Registrar(versao, "CNN 2 blocos (teste)", OrigemDados.Sintetico, escoreValidado,
                new string('b', 64), "{}", new DateOnly(2026, 11, 1));
            contexto.ModelosIA.Add(modelo);
        }

        modelo.Ativar(DateTimeOffset.UtcNow);
        return await contexto.SaveChangesAsync();
    });
}
