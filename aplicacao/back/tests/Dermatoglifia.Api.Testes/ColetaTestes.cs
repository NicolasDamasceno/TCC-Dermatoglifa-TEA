using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Infraestrutura.Persistencia;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dermatoglifia.Api.Testes;

/// <summary>UC06 de ponta a ponta: sessão, envio, validação e cifragem das imagens.</summary>
[Collection(ColecaoApi.Nome)]
public sealed class ColetaTestes(AmbienteApi ambiente)
{
    private static readonly (string Mao, string Dedo)[] OrdemColeta =
    [
        .. new[] { "POLEGAR", "INDICADOR", "MEDIO", "ANELAR", "MINIMO" }.Select(d => ("DIREITA", d)),
        .. new[] { "POLEGAR", "INDICADOR", "MEDIO", "ANELAR", "MINIMO" }.Select(d => ("ESQUERDA", d)),
    ];

    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly object NovaSessao = new { metodo = "LEITOR_OPTICO", tipoImpressao = "ROLADA", dispositivo = "Leitor sintético" };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage resposta)
        => await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteApi.Json);

    /// <summary>Médico aprovado + paciente com TCLE válido + sessão iniciada.</summary>
    private async Task<(MedicoDeTeste Medico, Guid PacienteId, Guid SessaoId)> SessaoIniciadaAsync()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        var pacienteId = await PacienteComTcleAsync(medico);
        var resposta = await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{pacienteId}/sessoes", NovaSessao);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (medico, pacienteId, (await JsonAsync(resposta)).GetProperty("id").GetGuid());
    }

    private static async Task<Guid> PacienteComTcleAsync(MedicoDeTeste medico, bool comTcle = true)
    {
        var paciente = await medico.Cliente.PostAsJsonAsync("/api/pacientes", new
        {
            nome = "Criança Sintética", dataNascimento = "2022-03-14", sexo = "FEMININO",
            responsavel = new { nome = "Responsável Sintético", parentesco = "PAI", contato = "(86) 90000-0000" },
        });
        var id = (await JsonAsync(paciente)).GetProperty("id").GetGuid();
        if (comTcle)
        {
            await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{id}/consentimentos", new
            {
                tipo = "TCLE", versaoTermo = "CEP-v1.0",
                assinadoEm = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                autorizaAnalise = true, cienteUsoIA = true, autorizaUsoTreino = false,
            });
        }

        return id;
    }

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, Guid sessaoId, string mao, string dedo, byte[] imagem)
        => cliente.PutAsync($"/api/sessoes/{sessaoId}/digitais/{mao}/{dedo}", ImagensSinteticas.Formulario(imagem));

    private async Task<List<(string Caminho, string Hash)>> ArquivosNoBancoAsync(Guid sessaoId)
        => await ambiente.NoEscopoAsync(async sp => (await sp.GetRequiredService<AppDbContext>().Digitais
                .Where(d => d.SessaoColetaId == sessaoId && d.Coletavel)
                .Select(d => new { d.CaminhoArquivo, d.HashSha256 })
                .ToListAsync())
            .Select(d => (d.CaminhoArquivo!, d.HashSha256!)).ToList());

    private string[] ArquivosNoDisco(Guid sessaoId)
    {
        var pasta = Path.Combine(ambiente.DiretorioImagens, sessaoId.ToString("N"));
        return Directory.Exists(pasta) ? Directory.GetFiles(pasta) : [];
    }

    [Fact]
    public async Task Fluxo_completo_dez_digitais_cifradas_e_sessao_completa()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();

        var inicial = await medico.Cliente.GetFromJsonAsync<JsonElement>($"/api/sessoes/{sessaoId}", AmbienteApi.Json);
        var dedos = inicial.GetProperty("dedos").EnumerateArray().ToList();
        Assert.Equal(10, dedos.Count);
        Assert.Equal(("DIREITA", "POLEGAR"), (dedos[0].GetProperty("mao").GetString(), dedos[0].GetProperty("dedo").GetString()));
        Assert.Equal(("ESQUERDA", "MINIMO"), (dedos[9].GetProperty("mao").GetString(), dedos[9].GetProperty("dedo").GetString()));
        Assert.All(dedos, d => Assert.Equal("PENDENTE", d.GetProperty("situacao").GetString()));
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow); // nascimento sintético: 14/03/2022
        var mesesEsperados = ((hoje.Year - 2022) * 12) + hoje.Month - 3 - (hoje.Day < 14 ? 1 : 0);
        Assert.Equal(mesesEsperados, inicial.GetProperty("idadeMeses").GetInt32());

        foreach (var (mao, dedo) in OrdemColeta)
        {
            Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(medico.Cliente, sessaoId, mao, dedo, ImagensSinteticas.Gerar())).StatusCode);
        }

        var conclusao = await medico.Cliente.PostAsync($"/api/sessoes/{sessaoId}/conclusao", null);
        Assert.Equal(HttpStatusCode.OK, conclusao.StatusCode);
        var final = await JsonAsync(conclusao);
        Assert.Equal("COMPLETA", final.GetProperty("status").GetString());
        Assert.All(final.GetProperty("dedos").EnumerateArray(), d =>
        {
            Assert.Equal("COLETADO", d.GetProperty("situacao").GetString());
            Assert.Equal(500, d.GetProperty("resolucaoDpi").GetInt32());
        });

        // No disco: 10 arquivos, nenhum legível como PNG sem a chave.
        var noBanco = await ArquivosNoBancoAsync(sessaoId);
        Assert.Equal(10, noBanco.Count);
        Assert.Equal(10, ArquivosNoDisco(sessaoId).Length);
        var armazenamento = ambiente.Fabrica.Services.GetRequiredService<IArmazenamentoImagens>();
        foreach (var (caminho, hash) in noBanco)
        {
            var bruto = await File.ReadAllBytesAsync(Path.Combine(ambiente.DiretorioImagens, caminho));
            Assert.True(bruto.AsSpan().IndexOf(AssinaturaPng) < 0); // nada do PNG em claro no disco

            var claro = await armazenamento.AbrirAsync(caminho);
            Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(claro)));
            using var imagem = new MagickImage(claro);
            Assert.Equal(MagickFormat.Png, imagem.Format);
            Assert.Equal(ColorSpace.Gray, imagem.ColorSpace);
            Assert.Equal(8u, imagem.Depth);
            Assert.Equal(500, Math.Round(imagem.Density.ChangeUnits(DensityUnit.PixelsPerInch).X));
        }
    }

    [Theory]
    [InlineData("BMP")]
    [InlineData("TIFF")]
    public async Task Bmp_e_tiff_sao_aceitos_e_viram_png(string formato)
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();
        var magick = formato == "BMP" ? MagickFormat.Bmp3 : MagickFormat.Tiff;

        var resposta = await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", ImagensSinteticas.Gerar(magick));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.EndsWith(".png.enc", Assert.Single(await ArquivosNoBancoAsync(sessaoId)).Caminho, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Imagem_colorida_e_convertida_para_tons_de_cinza()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();

        await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", ImagensSinteticas.Gerar(colorida: true));

        var (caminho, _) = Assert.Single(await ArquivosNoBancoAsync(sessaoId));
        var claro = await ambiente.Fabrica.Services.GetRequiredService<IArmazenamentoImagens>().AbrirAsync(caminho);
        using var imagem = new MagickImage(claro);
        Assert.Equal(ColorSpace.Gray, imagem.ColorSpace);
    }

    [Fact]
    public async Task Formatos_nao_aceitos_retornam_415()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();

        var jpeg = await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", ImagensSinteticas.Gerar(MagickFormat.Jpeg));
        var lixo = await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", RandomNumberGenerator.GetBytes(2048));
        var pngQuebrado = await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR",
            [.. ImagensSinteticas.Gerar().Take(200)]);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, jpeg.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, lixo.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, pngQuebrado.StatusCode);
        Assert.Empty(ArquivosNoDisco(sessaoId));
    }

    [Fact]
    public async Task Resolucao_baixa_ausente_ou_imagem_minuscula_retornam_422()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();

        var baixa = await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", ImagensSinteticas.Gerar(ppi: 300));
        var ausente = await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", ImagensSinteticas.Gerar(comResolucao: false));
        var minuscula = await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", ImagensSinteticas.Gerar(lado: 50));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, baixa.StatusCode);
        Assert.Contains("300 ppi", (await JsonAsync(baixa)).GetProperty("title").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, ausente.StatusCode);
        Assert.Contains("não informa a resolução", (await JsonAsync(ausente)).GetProperty("title").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, minuscula.StatusCode);
        Assert.Empty(ArquivosNoDisco(sessaoId));
    }

    [Fact]
    public async Task Arquivo_acima_de_5_mb_retorna_413()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();
        var grande = new byte[(5 * 1024 * 1024) + 1];
        ImagensSinteticas.Gerar().AsSpan(0, 8).CopyTo(grande); // assinatura PNG

        var resposta = await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", grande);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resposta.StatusCode);
    }

    [Fact]
    public async Task Refazer_um_dedo_substitui_o_arquivo_e_apaga_o_anterior()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();
        await EnviarAsync(medico.Cliente, sessaoId, "ESQUERDA", "MEDIO", ImagensSinteticas.Gerar());
        var (primeiro, hashPrimeiro) = Assert.Single(await ArquivosNoBancoAsync(sessaoId));

        await EnviarAsync(medico.Cliente, sessaoId, "ESQUERDA", "MEDIO", ImagensSinteticas.Gerar());

        var (segundo, hashSegundo) = Assert.Single(await ArquivosNoBancoAsync(sessaoId));
        Assert.NotEqual(primeiro, segundo);
        Assert.NotEqual(hashPrimeiro, hashSegundo);
        Assert.False(File.Exists(Path.Combine(ambiente.DiretorioImagens, primeiro)));
        Assert.Single(ArquivosNoDisco(sessaoId));
    }

    [Fact]
    public async Task Dedo_nao_coletavel_conclui_como_incompleta_e_apaga_imagem_anterior()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();
        foreach (var (mao, dedo) in OrdemColeta)
        {
            await EnviarAsync(medico.Cliente, sessaoId, mao, dedo, ImagensSinteticas.Gerar());
        }

        var marcar = await medico.Cliente.PostAsJsonAsync(
            $"/api/sessoes/{sessaoId}/digitais/ESQUERDA/ANELAR/nao-coletavel", new { motivo = "Lesão no dedo" });
        var conclusao = await JsonAsync(await medico.Cliente.PostAsync($"/api/sessoes/{sessaoId}/conclusao", null));

        Assert.Equal(HttpStatusCode.OK, marcar.StatusCode);
        Assert.Equal("INCOMPLETA", conclusao.GetProperty("status").GetString());
        var anelar = conclusao.GetProperty("dedos").EnumerateArray()
            .Single(d => d.GetProperty("mao").GetString() == "ESQUERDA" && d.GetProperty("dedo").GetString() == "ANELAR");
        Assert.Equal("NAO_COLETAVEL", anelar.GetProperty("situacao").GetString());
        Assert.Equal("Lesão no dedo", anelar.GetProperty("motivoNaoColetavel").GetString());
        Assert.Equal(9, ArquivosNoDisco(sessaoId).Length);
    }

    [Fact]
    public async Task Concluir_com_dedos_pendentes_e_enviar_apos_concluir_sao_recusados()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();

        var prematura = await medico.Cliente.PostAsync($"/api/sessoes/{sessaoId}/conclusao", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, prematura.StatusCode);

        foreach (var (mao, dedo) in OrdemColeta)
        {
            await EnviarAsync(medico.Cliente, sessaoId, mao, dedo, ImagensSinteticas.Gerar());
        }

        await medico.Cliente.PostAsync($"/api/sessoes/{sessaoId}/conclusao", null);
        var depois = await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", ImagensSinteticas.Gerar());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, depois.StatusCode);
        Assert.Equal(10, ArquivosNoDisco(sessaoId).Length);
    }

    [Fact]
    public async Task Sessao_exige_consentimento_e_so_uma_em_andamento()
    {
        var medico = await ambiente.MedicoAprovadoAsync();
        var semTcle = await PacienteComTcleAsync(medico, comTcle: false);
        var comTcle = await PacienteComTcleAsync(medico);

        var negada = await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{semTcle}/sessoes", NovaSessao);
        var primeira = await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{comTcle}/sessoes", NovaSessao);
        var segunda = await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{comTcle}/sessoes", NovaSessao);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, negada.StatusCode);
        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);

        var primeiraId = (await JsonAsync(primeira)).GetProperty("id").GetGuid();
        var cancelada = await JsonAsync(await medico.Cliente.PostAsync($"/api/sessoes/{primeiraId}/cancelamento", null));
        Assert.Equal("CANCELADA", cancelada.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Created, (await medico.Cliente.PostAsJsonAsync($"/api/pacientes/{comTcle}/sessoes", NovaSessao)).StatusCode);
    }

    [Fact]
    public async Task Revogar_o_consentimento_interrompe_a_coleta()
    {
        var (medico, pacienteId, sessaoId) = await SessaoIniciadaAsync();
        await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", ImagensSinteticas.Gerar());
        var detalhe = await medico.Cliente.GetFromJsonAsync<JsonElement>($"/api/pacientes/{pacienteId}", AmbienteApi.Json);
        var tcle = detalhe.GetProperty("consentimentos")[0].GetProperty("id").GetGuid();

        await medico.Cliente.PostAsync($"/api/pacientes/{pacienteId}/consentimentos/{tcle}/revogacao", null);
        var resposta = await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "INDICADOR", ImagensSinteticas.Gerar());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Single(ArquivosNoDisco(sessaoId));
    }

    [Fact]
    public async Task Outro_medico_nao_acessa_a_sessao_nem_o_paciente()
    {
        var (_, pacienteId, sessaoId) = await SessaoIniciadaAsync();
        var outro = await ambiente.MedicoAprovadoAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await outro.Cliente.GetAsync($"/api/sessoes/{sessaoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await EnviarAsync(outro.Cliente, sessaoId, "DIREITA", "POLEGAR", ImagensSinteticas.Gerar())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outro.Cliente.PostAsJsonAsync($"/api/pacientes/{pacienteId}/sessoes", NovaSessao)).StatusCode);
        Assert.Empty(ArquivosNoDisco(sessaoId));
    }

    [Fact]
    public async Task Mao_ou_dedo_invalidos_na_rota_retornam_400()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();

        var resposta = await EnviarAsync(medico.Cliente, sessaoId, "MEIO", "POLEGAR", ImagensSinteticas.Gerar());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Arquivo_adulterado_ou_trocado_de_dedo_nao_decifra()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();
        await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "POLEGAR", ImagensSinteticas.Gerar());
        await EnviarAsync(medico.Cliente, sessaoId, "DIREITA", "INDICADOR", ImagensSinteticas.Gerar());
        var arquivos = (await ArquivosNoBancoAsync(sessaoId)).Select(a => a.Caminho).OrderBy(c => c, StringComparer.Ordinal).ToList();
        var armazenamento = ambiente.Fabrica.Services.GetRequiredService<IArmazenamentoImagens>();
        var (indicador, polegar) = (arquivos[0], arquivos[1]); // DIREITA_INDICADOR < DIREITA_POLEGAR

        // Um bit alterado no conteúdo.
        var caminhoPolegar = Path.Combine(ambiente.DiretorioImagens, polegar);
        var bytes = await File.ReadAllBytesAsync(caminhoPolegar);
        bytes[^1] ^= 0x01;
        await File.WriteAllBytesAsync(caminhoPolegar, bytes);
        await Assert.ThrowsAnyAsync<CryptographicException>(() => armazenamento.AbrirAsync(polegar));

        // O arquivo do indicador copiado para o lugar do polegar.
        File.Copy(Path.Combine(ambiente.DiretorioImagens, indicador), caminhoPolegar, overwrite: true);
        await Assert.ThrowsAnyAsync<CryptographicException>(() => armazenamento.AbrirAsync(polegar));
    }

    [Fact]
    public async Task Auditoria_registra_envios_sem_dados_da_imagem()
    {
        var (medico, _, sessaoId) = await SessaoIniciadaAsync();
        await EnviarAsync(medico.Cliente, sessaoId, "ESQUERDA", "MINIMO", ImagensSinteticas.Gerar());

        var registros = await ambiente.NoEscopoAsync(sp => sp.GetRequiredService<AppDbContext>().Auditoria
            .Where(a => a.EntidadeId == sessaoId)
            .Select(a => new { a.Acao, a.Detalhes, a.UsuarioId })
            .ToListAsync());

        Assert.Contains(registros, r => r.Acao == "SESSAO_INICIADA");
        var envio = Assert.Single(registros, r => r.Acao == "DIGITAL_ENVIADA");
        Assert.Equal(medico.Id, envio.UsuarioId);
        using var detalhes = JsonDocument.Parse(envio.Detalhes!);
        Assert.Equal("ESQUERDA", detalhes.RootElement.GetProperty("mao").GetString());
        Assert.Equal("MINIMO", detalhes.RootElement.GetProperty("dedo").GetString());
        Assert.Equal(2, detalhes.RootElement.EnumerateObject().Count());
    }
}
