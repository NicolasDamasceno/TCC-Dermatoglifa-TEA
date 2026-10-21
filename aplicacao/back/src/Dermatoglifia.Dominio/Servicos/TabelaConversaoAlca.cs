using Dermatoglifia.Dominio.Enums;

namespace Dermatoglifia.Dominio.Servicos;

/// <summary>
/// Converte a classe prevista pela CNN no espaço da imagem (alça esquerda/direita)
/// no padrão dermatoglífico (alça ulnar/radial), usando a mão registrada.
/// <para>
/// Ulnar = alça que se abre para o lado do dedo mínimo; radial = para o lado do
/// polegar. Na convenção das impressões (tinta ou leitor que reproduz a
/// impressão), uma alça que se abre para a direita é ulnar na mão direita e
/// radial na esquerda. Se o dispositivo de captura gerar imagens espelhadas,
/// usar <see cref="Espelhada"/>. A convenção precisa ser confirmada com o
/// dispositivo escolhido (ADR-0002, ação 3; ADR-0004, ação 3).
/// </para>
/// </summary>
public sealed class TabelaConversaoAlca
{
    private readonly bool _alcaDireitaEhUlnarNaMaoDireita;

    private TabelaConversaoAlca(bool alcaDireitaEhUlnarNaMaoDireita)
        => _alcaDireitaEhUlnarNaMaoDireita = alcaDireitaEhUlnarNaMaoDireita;

    /// <summary>Convenção das impressões: alça para a direita na mão direita é ulnar.</summary>
    public static TabelaConversaoAlca Impressao { get; } = new(alcaDireitaEhUlnarNaMaoDireita: true);

    /// <summary>Para dispositivos que entregam a imagem espelhada.</summary>
    public static TabelaConversaoAlca Espelhada { get; } = new(alcaDireitaEhUlnarNaMaoDireita: false);

    public PadraoDigital Converter(ClasseImagem classe, Mao mao) => classe switch
    {
        ClasseImagem.Arco => PadraoDigital.Arco,
        ClasseImagem.ArcoTendido => PadraoDigital.ArcoTendido,
        ClasseImagem.Verticilo => PadraoDigital.Verticilo,
        ClasseImagem.AlcaDireita => EhUlnar(abreParaDireita: true, mao) ? PadraoDigital.AlcaUlnar : PadraoDigital.AlcaRadial,
        ClasseImagem.AlcaEsquerda => EhUlnar(abreParaDireita: false, mao) ? PadraoDigital.AlcaUlnar : PadraoDigital.AlcaRadial,
        _ => throw new ArgumentOutOfRangeException(nameof(classe), classe, "Classe de imagem desconhecida."),
    };

    private bool EhUlnar(bool abreParaDireita, Mao mao)
    {
        // Na convenção padrão, "abre para a direita" + "mão direita" = ulnar;
        // trocar a mão ou o sentido inverte o resultado; a convenção espelhada
        // inverte tudo mais uma vez.
        var ulnarNaConvencaoImpressao = abreParaDireita == (mao == Mao.Direita);
        return ulnarNaConvencaoImpressao == _alcaDireitaEhUlnarNaMaoDireita;
    }
}
