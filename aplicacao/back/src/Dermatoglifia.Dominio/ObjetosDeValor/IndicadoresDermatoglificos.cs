using Dermatoglifia.Dominio.Enums;

namespace Dermatoglifia.Dominio.ObjetosDeValor;

/// <summary>
/// Indicadores derivados dos padrões dos 10 dedos (ADR-0002). O back-end os
/// recalcula para conferir a resposta do Serviço de IA (doc 08, seção 7).
/// </summary>
public sealed record IndicadoresDermatoglificos(
    int QtdArcos,
    int QtdArcosTendidos,
    int QtdAlcasUlnares,
    int QtdAlcasRadiais,
    int QtdVerticilos,
    int DiscordanciaBilateral,
    int DiscordanciaBilateralComPolegar)
{
    /// <summary>
    /// Calcula os indicadores a partir do padrão de cada um dos 10 dedos.
    /// <para>
    /// <see cref="DiscordanciaBilateral"/>: pares homólogos (indicador, médio,
    /// anelar e mínimo) com padrões diferentes entre as mãos, de 0 a 4 — polegar
    /// excluído, como em de Bruin et al. (2014).
    /// <see cref="DiscordanciaBilateralComPolegar"/>: a mesma contagem com o
    /// polegar, de 0 a 5.
    /// </para>
    /// </summary>
    public static IndicadoresDermatoglificos Calcular(IReadOnlyDictionary<PosicaoDedo, PadraoDigital> padroes)
    {
        ArgumentNullException.ThrowIfNull(padroes);
        Regra.Exigir(
            padroes.Count == PosicaoDedo.TotalDedos && PosicaoDedo.OrdemColeta.All(padroes.ContainsKey),
            "Os indicadores exigem o padrão dos 10 dedos.");

        int Contar(PadraoDigital padrao) => padroes.Values.Count(p => p == padrao);

        int Discordantes(IEnumerable<Dedo> dedos) => dedos.Count(dedo =>
            padroes[new PosicaoDedo(Mao.Direita, dedo)] != padroes[new PosicaoDedo(Mao.Esquerda, dedo)]);

        var semPolegar = Enum.GetValues<Dedo>().Where(d => d != Dedo.Polegar).ToArray();

        return new IndicadoresDermatoglificos(
            QtdArcos: Contar(PadraoDigital.Arco),
            QtdArcosTendidos: Contar(PadraoDigital.ArcoTendido),
            QtdAlcasUlnares: Contar(PadraoDigital.AlcaUlnar),
            QtdAlcasRadiais: Contar(PadraoDigital.AlcaRadial),
            QtdVerticilos: Contar(PadraoDigital.Verticilo),
            DiscordanciaBilateral: Discordantes(semPolegar),
            DiscordanciaBilateralComPolegar: Discordantes(Enum.GetValues<Dedo>()));
    }
}
