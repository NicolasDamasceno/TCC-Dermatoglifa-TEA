using Dermatoglifia.Dominio.Enums;

namespace Dermatoglifia.Dominio.Entidades;

/// <summary>Versão de modelo registrada (doc 05; ADR-0005).</summary>
public sealed class ModeloIA : Entidade
{
    public string Versao { get; private set; } = null!;
    public string Arquitetura { get; private set; } = null!;
    public OrigemDados OrigemDados { get; private set; }

    /// <summary>
    /// Só verdadeiro quando a Etapa 2 desta versão foi validada; enquanto falso,
    /// o escore recebido é descartado (RN10).
    /// </summary>
    public bool EscoreValidado { get; private set; }

    public string HashSha256 { get; private set; } = null!;

    /// <summary>JSON com as métricas do conjunto de teste (cópia de metricas.json).</summary>
    public string MetricasTeste { get; private set; } = null!;

    public DateOnly TreinadoEm { get; private set; }
    public bool Ativo { get; private set; }
    public DateTimeOffset? PromovidoEm { get; private set; }

    private ModeloIA() { } // EF Core

    public static ModeloIA Registrar(
        string versao,
        string arquitetura,
        OrigemDados origemDados,
        bool escoreValidado,
        string hashSha256,
        string metricasTesteJson,
        DateOnly treinadoEm)
    {
        var hash = Regra.TextoObrigatorio(hashSha256, "Hash", 64).ToLowerInvariant();
        Regra.Exigir(hash.Length == 64 && hash.All(Uri.IsHexDigit), "Hash SHA-256 inválido.");

        return new ModeloIA
        {
            Versao = Regra.TextoObrigatorio(versao, "Versão", 100),
            Arquitetura = Regra.TextoObrigatorio(arquitetura, "Arquitetura", 100),
            OrigemDados = origemDados,
            EscoreValidado = escoreValidado,
            HashSha256 = hash,
            MetricasTeste = Regra.TextoObrigatorio(metricasTesteJson, "Métricas de teste", 100_000),
            TreinadoEm = treinadoEm,
        };
    }

    /// <summary>
    /// Marca como ativo. Desativar o anterior é responsabilidade de quem promove;
    /// o banco garante um único ativo por índice parcial único.
    /// </summary>
    public void Ativar(DateTimeOffset agora)
    {
        Ativo = true;
        PromovidoEm = agora;
    }

    public void Desativar() => Ativo = false;
}
