using Dermatoglifia.Dominio.Enums;

namespace Dermatoglifia.Dominio.ObjetosDeValor;

/// <summary>Um dos 10 dedos: mão + dedo.</summary>
public readonly record struct PosicaoDedo(Mao Mao, Dedo Dedo)
{
    /// <summary>
    /// Ordem fixa de coleta (UC06): mão direita do polegar ao mínimo, depois a
    /// mão esquerda na mesma sequência.
    /// </summary>
    public static IReadOnlyList<PosicaoDedo> OrdemColeta { get; } =
    [
        .. Enum.GetValues<Dedo>().Select(d => new PosicaoDedo(Mao.Direita, d)),
        .. Enum.GetValues<Dedo>().Select(d => new PosicaoDedo(Mao.Esquerda, d)),
    ];

    public const int TotalDedos = 10;

    /// <summary>Nome usado no armazenamento e no contrato com a IA, ex.: DIREITA_POLEGAR.</summary>
    public string NomeArquivo => $"{Mao.ToString().ToUpperInvariant()}_{Dedo.ToString().ToUpperInvariant()}";

    /// <summary>O mesmo dedo na outra mão.</summary>
    public PosicaoDedo Homologo => new(Mao == Mao.Direita ? Mao.Esquerda : Mao.Direita, Dedo);

    public override string ToString() => NomeArquivo;
}
