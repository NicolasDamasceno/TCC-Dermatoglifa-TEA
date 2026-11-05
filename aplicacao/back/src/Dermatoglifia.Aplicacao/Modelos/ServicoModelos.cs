using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Aplicacao.Excecoes;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.Excecoes;
using Microsoft.EntityFrameworkCore;

namespace Dermatoglifia.Aplicacao.Modelos;

/// <summary>
/// UC10 — Promover versão do modelo. O administrador envia o <c>config.json</c> e o
/// <c>metricas.json</c> da versão (gerados pelo treino em <c>treino/modelos/</c>); a
/// versão e o hash vêm do próprio Serviço de IA (<c>GET /saude</c>), que já conferiu
/// o hash ao carregar o modelo. Assim só é ativada a versão que de fato responde às
/// análises, com o hash do arquivo carregado (RNF09).
/// </summary>
public sealed partial class ServicoModelos(
    IContextoDados contexto,
    IClienteInferencia inferencia,
    IAuditoria auditoria,
    IRelogio relogio)
{
    /// <summary>Tamanho máximo de cada JSON enviado (o do treino tem poucos KB).</summary>
    public const int TamanhoMaximoJson = 1024 * 1024;

    public async Task<PainelModelos> ObterAsync(CancellationToken cancelamento = default)
    {
        var modelos = await contexto.ModelosIA.AsNoTracking()
            .OrderByDescending(m => m.Ativo)
            .ThenByDescending(m => m.PromovidoEm)
            .Select(m => new
            {
                Modelo = m,
                Analises = contexto.Analises.Count(a => a.ModeloIAId == m.Id),
            })
            .ToListAsync(cancelamento);

        ServicoIA servico;
        try
        {
            var saude = await inferencia.ConsultarSaudeAsync(cancelamento);
            var registrado = saude.Disponivel ? modelos.Select(m => m.Modelo).SingleOrDefault(m => m.Versao == saude.VersaoModelo) : null;
            servico = new ServicoIA(
                saude.Disponivel, saude.VersaoModelo, saude.OrigemDados, saude.Sha256, saude.PastaOrigem, saude.Motivo,
                Registrado: registrado is not null,
                Ativo: registrado?.Ativo == true,
                HashConfere: registrado is null || registrado.HashSha256 == saude.Sha256);
        }
        catch (FalhaInferenciaException e)
        {
            servico = new ServicoIA(false, null, null, null, null, e.Message, false, false, true);
        }

        return new PainelModelos(servico, modelos.Select(m => Resumir(m.Modelo, m.Analises)).ToList());
    }

    public async Task<ModeloDto> PromoverAsync(ArquivosVersao arquivos, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(arquivos);
        var config = LerConfig(arquivos.Config);
        var metricas = LerMetricas(arquivos.Metricas);

        SaudeInferencia saude;
        try
        {
            saude = await inferencia.ConsultarSaudeAsync(cancelamento);
        }
        catch (FalhaInferenciaException e)
        {
            throw new RegraDeNegocioException($"Não foi possível consultar o Serviço de IA: {e.Message}");
        }

        if (!saude.Disponivel)
        {
            throw new RegraDeNegocioException($"O Serviço de IA está sem modelo: {saude.Motivo}");
        }

        if (saude.VersaoModelo != config.Versao)
        {
            throw new RegraDeNegocioException(
                $"O Serviço de IA está com a versão \"{saude.VersaoModelo}\", não \"{config.Versao}\". " +
                "Copie a pasta do modelo para o serviço e reinicie-o antes de promover.");
        }

        if (saude.OrigemDados != config.OrigemDados)
        {
            throw new RegraDeNegocioException("A origem dos dados no config.json não confere com a do Serviço de IA.");
        }

        var modelo = await contexto.ModelosIA.SingleOrDefaultAsync(m => m.Versao == config.Versao, cancelamento);
        var resultado = "REGISTRADO";
        if (modelo is null)
        {
            // Etapa 2 ainda não validada: o escore nunca é exibido por esta rota (RN10).
            modelo = ModeloIA.Registrar(config.Versao, config.Arquitetura, config.OrigemDados, escoreValidado: false,
                saude.Sha256!, metricas, config.TreinadoEm);
            contexto.ModelosIA.Add(modelo);
        }
        else
        {
            if (modelo.HashSha256 != saude.Sha256)
            {
                throw new RegraDeNegocioException(
                    "Esta versão já foi registrada com outro arquivo (hash diferente). Treine com outra versão.");
            }

            if (modelo.Ativo)
            {
                throw new RegraDeNegocioException("Esta versão já é o modelo ativo.");
            }

            resultado = "REATIVADO";
        }

        // Desativar antes de ativar: o índice parcial único admite um só ativo (doc 05).
        await using var transacao = await contexto.IniciarTransacaoAsync(cancelamento);
        foreach (var ativo in await contexto.ModelosIA.Where(m => m.Ativo).ToListAsync(cancelamento))
        {
            ativo.Desativar();
        }

        await contexto.SalvarAsync(cancelamento);
        modelo.Ativar(relogio.Agora);
        auditoria.Registrar("MODELO_PROMOVIDO", "modelo_ia", modelo.Id,
            new Dictionary<string, string> { ["versao_modelo"] = modelo.Versao, ["resultado"] = resultado });
        await contexto.SalvarAsync(cancelamento);
        await transacao.CommitAsync(cancelamento);

        var analises = await contexto.Analises.CountAsync(a => a.ModeloIAId == modelo.Id, cancelamento);
        return Resumir(modelo, analises);
    }

    private static ConfigVersao LerConfig(byte[] conteudo)
    {
        using var documento = LerJson(conteudo, "config.json");
        var raiz = documento.RootElement;
        var versao = Texto(raiz, "versao", "config.json");
        if (!VersaoValida().IsMatch(versao))
        {
            throw new RegraDeNegocioException("config.json: versão com caracteres inválidos.");
        }

        var origem = Texto(raiz, "origem_dados", "config.json") switch
        {
            "SINTETICO" => OrigemDados.Sintetico,
            "REAL" => OrigemDados.Real,
            _ => throw new RegraDeNegocioException("config.json: origem_dados deve ser SINTETICO ou REAL."),
        };

        if (!DateOnly.TryParseExact(Texto(raiz, "treinado_em", "config.json"), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var treinadoEm))
        {
            throw new RegraDeNegocioException("config.json: treinado_em deve estar no formato AAAA-MM-DD.");
        }

        return new ConfigVersao(versao, Texto(raiz, "arquitetura", "config.json"), origem, treinadoEm);
    }

    /// <summary>Resumo do teste guardado em <c>metricas_teste</c> (mesmo formato do registro.sql do treino).</summary>
    private static string LerMetricas(byte[] conteudo)
    {
        using var documento = LerJson(conteudo, "metricas.json");
        if (!documento.RootElement.TryGetProperty("teste", out var teste) || teste.ValueKind != JsonValueKind.Object)
        {
            throw new RegraDeNegocioException("metricas.json: falta o bloco \"teste\".");
        }

        var resumo = new Dictionary<string, object>
        {
            ["acuracia"] = Math.Round(Numero(teste, "acuracia"), 4),
            ["acuraciaBalanceada"] = Math.Round(Numero(teste, "acuracia_balanceada"), 4),
            ["nTeste"] = (int)Numero(teste, "imagens"),
        };
        if (teste.TryGetProperty("auc_macro_um_contra_todos", out var auc) && auc.ValueKind == JsonValueKind.Number)
        {
            resumo["auc"] = Math.Round(auc.GetDouble(), 4);
        }

        return JsonSerializer.Serialize(resumo);
    }

    private static JsonDocument LerJson(byte[] conteudo, string nome)
    {
        if (conteudo.Length is 0 or > TamanhoMaximoJson)
        {
            throw new RegraDeNegocioException($"{nome}: arquivo vazio ou maior que 1 MB.");
        }

        try
        {
            var documento = JsonDocument.Parse(conteudo);
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
            {
                documento.Dispose();
                throw new RegraDeNegocioException($"{nome}: esperado um objeto JSON.");
            }

            return documento;
        }
        catch (JsonException)
        {
            throw new RegraDeNegocioException($"{nome}: não é um JSON válido.");
        }
    }

    private static string Texto(JsonElement objeto, string campo, string nome)
        => objeto.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String
           && !string.IsNullOrWhiteSpace(valor.GetString())
            ? valor.GetString()!.Trim()
            : throw new RegraDeNegocioException($"{nome}: falta o campo \"{campo}\".");

    private static double Numero(JsonElement objeto, string campo)
        => objeto.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.Number
            ? valor.GetDouble()
            : throw new RegraDeNegocioException($"metricas.json: falta o número \"teste.{campo}\".");

    private static ModeloDto Resumir(ModeloIA m, int analises)
    {
        using var metricas = JsonDocument.Parse(m.MetricasTeste);
        return new ModeloDto(m.Id, m.Versao, m.Arquitetura, m.OrigemDados, m.EscoreValidado, m.HashSha256,
            metricas.RootElement.Clone(), m.TreinadoEm, m.Ativo, m.PromovidoEm, analises);
    }

    /// <summary>Mesma regra de nome de versão do treino (<c>VERSAO_VALIDA</c> em etapa1.py).</summary>
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]{2,99}$")]
    private static partial Regex VersaoValida();

    private sealed record ConfigVersao(string Versao, string Arquitetura, OrigemDados OrigemDados, DateOnly TreinadoEm);
}

public sealed record ArquivosVersao(byte[] Config, byte[] Metricas);

/// <summary>O que o Serviço de IA está usando agora e como isso se compara ao registro.</summary>
public sealed record ServicoIA(
    bool Disponivel,
    string? VersaoModelo,
    OrigemDados? OrigemDados,
    string? Sha256,
    string? PastaOrigem,
    string? Motivo,
    bool Registrado,
    bool Ativo,
    bool HashConfere);

public sealed record ModeloDto(
    Guid Id,
    string Versao,
    string Arquitetura,
    OrigemDados OrigemDados,
    bool EscoreValidado,
    string HashSha256,
    JsonElement Metricas,
    DateOnly TreinadoEm,
    bool Ativo,
    DateTimeOffset? PromovidoEm,
    int Analises);

public sealed record PainelModelos(ServicoIA Servico, IReadOnlyList<ModeloDto> Modelos);
