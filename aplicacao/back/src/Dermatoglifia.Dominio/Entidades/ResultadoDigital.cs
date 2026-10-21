using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.ObjetosDeValor;

namespace Dermatoglifia.Dominio.Entidades;

/// <summary>Classificação de uma digital pela CNN (Etapa 1), já convertida para ulnar/radial.</summary>
public sealed class ResultadoDigital : Entidade
{
    public Guid AnaliseId { get; private set; }
    public Guid DigitalId { get; private set; }
    public ClasseImagem ClasseImagem { get; private set; }
    public PadraoDigital PadraoPrevisto { get; private set; }
    public decimal Confianca { get; private set; }
    public IReadOnlyDictionary<ClasseImagem, decimal> Probabilidades { get; private set; } = null!;
    public bool AlertaQualidade { get; private set; }

    private ResultadoDigital() { } // EF Core

    internal static ResultadoDigital Criar(Guid analiseId, Guid digitalId, ResultadoDedoRecebido recebido, PadraoDigital padrao)
        => new()
        {
            AnaliseId = analiseId,
            DigitalId = digitalId,
            ClasseImagem = recebido.ClasseImagem,
            PadraoPrevisto = padrao,
            Confianca = recebido.Confianca,
            Probabilidades = new Dictionary<ClasseImagem, decimal>(recebido.Probabilidades),
            AlertaQualidade = recebido.AlertaQualidade,
        };
}

/// <summary>
/// Resultado de um dedo como chegou do Serviço de IA. A <see cref="Analise"/>
/// confere a consistência antes de aceitá-lo (doc 08, seção 7).
/// </summary>
public sealed record ResultadoDedoRecebido(
    PosicaoDedo Posicao,
    ClasseImagem ClasseImagem,
    PadraoDigital PadraoInformado,
    decimal Confianca,
    IReadOnlyDictionary<ClasseImagem, decimal> Probabilidades,
    bool AlertaQualidade);
