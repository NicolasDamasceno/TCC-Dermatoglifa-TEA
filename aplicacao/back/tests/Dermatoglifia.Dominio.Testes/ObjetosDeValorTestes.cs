using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.Excecoes;
using Dermatoglifia.Dominio.ObjetosDeValor;
using Dermatoglifia.Dominio.Servicos;

namespace Dermatoglifia.Dominio.Testes;

public class PosicaoDedoTestes
{
    [Fact]
    public void OrdemColeta_tem_dez_dedos_mao_direita_primeiro()
    {
        var ordem = PosicaoDedo.OrdemColeta;

        Assert.Equal(10, ordem.Count);
        Assert.Equal(new PosicaoDedo(Mao.Direita, Dedo.Polegar), ordem[0]);
        Assert.Equal(new PosicaoDedo(Mao.Direita, Dedo.Minimo), ordem[4]);
        Assert.Equal(new PosicaoDedo(Mao.Esquerda, Dedo.Polegar), ordem[5]);
        Assert.Equal(new PosicaoDedo(Mao.Esquerda, Dedo.Minimo), ordem[9]);
    }

    [Fact]
    public void NomeArquivo_segue_o_contrato_com_a_IA()
    {
        Assert.Equal("DIREITA_POLEGAR", new PosicaoDedo(Mao.Direita, Dedo.Polegar).NomeArquivo);
        Assert.Equal("ESQUERDA_MEDIO", new PosicaoDedo(Mao.Esquerda, Dedo.Medio).NomeArquivo);
    }
}

public class TabelaConversaoAlcaTestes
{
    [Theory]
    [InlineData(ClasseImagem.AlcaDireita, Mao.Direita, PadraoDigital.AlcaUlnar)]
    [InlineData(ClasseImagem.AlcaDireita, Mao.Esquerda, PadraoDigital.AlcaRadial)]
    [InlineData(ClasseImagem.AlcaEsquerda, Mao.Esquerda, PadraoDigital.AlcaUlnar)]
    [InlineData(ClasseImagem.AlcaEsquerda, Mao.Direita, PadraoDigital.AlcaRadial)]
    [InlineData(ClasseImagem.Arco, Mao.Direita, PadraoDigital.Arco)]
    [InlineData(ClasseImagem.ArcoTendido, Mao.Esquerda, PadraoDigital.ArcoTendido)]
    [InlineData(ClasseImagem.Verticilo, Mao.Direita, PadraoDigital.Verticilo)]
    public void Convencao_de_impressao(ClasseImagem classe, Mao mao, PadraoDigital esperado)
        => Assert.Equal(esperado, TabelaConversaoAlca.Impressao.Converter(classe, mao));

    [Theory]
    [InlineData(ClasseImagem.AlcaDireita, Mao.Direita, PadraoDigital.AlcaRadial)]
    [InlineData(ClasseImagem.AlcaEsquerda, Mao.Esquerda, PadraoDigital.AlcaRadial)]
    [InlineData(ClasseImagem.Verticilo, Mao.Esquerda, PadraoDigital.Verticilo)]
    public void Convencao_espelhada_inverte_apenas_as_alcas(ClasseImagem classe, Mao mao, PadraoDigital esperado)
        => Assert.Equal(esperado, TabelaConversaoAlca.Espelhada.Converter(classe, mao));
}

public class IndicadoresDermatoglificosTestes
{
    private static Dictionary<PosicaoDedo, PadraoDigital> Todos(PadraoDigital padrao)
        => PosicaoDedo.OrdemColeta.ToDictionary(p => p, _ => padrao);

    [Fact]
    public void Maos_identicas_nao_tem_discordancia()
    {
        var indicadores = IndicadoresDermatoglificos.Calcular(Todos(PadraoDigital.AlcaUlnar));

        Assert.Equal(10, indicadores.QtdAlcasUlnares);
        Assert.Equal(0, indicadores.DiscordanciaBilateral);
        Assert.Equal(0, indicadores.DiscordanciaBilateralComPolegar);
    }

    [Fact]
    public void Polegar_discordante_conta_so_na_variante_com_polegar()
    {
        var padroes = Todos(PadraoDigital.AlcaUlnar);
        padroes[new PosicaoDedo(Mao.Esquerda, Dedo.Polegar)] = PadraoDigital.Verticilo;

        var indicadores = IndicadoresDermatoglificos.Calcular(padroes);

        Assert.Equal(0, indicadores.DiscordanciaBilateral);
        Assert.Equal(1, indicadores.DiscordanciaBilateralComPolegar);
        Assert.Equal(1, indicadores.QtdVerticilos);
        Assert.Equal(9, indicadores.QtdAlcasUlnares);
    }

    [Fact]
    public void Discordancia_maxima_e_quatro_sem_polegar_e_cinco_com_polegar()
    {
        var padroes = Todos(PadraoDigital.AlcaUlnar);
        foreach (var dedo in Enum.GetValues<Dedo>())
        {
            padroes[new PosicaoDedo(Mao.Esquerda, dedo)] = PadraoDigital.Arco;
        }

        var indicadores = IndicadoresDermatoglificos.Calcular(padroes);

        Assert.Equal(4, indicadores.DiscordanciaBilateral);
        Assert.Equal(5, indicadores.DiscordanciaBilateralComPolegar);
        Assert.Equal(5, indicadores.QtdArcos);
    }

    [Fact]
    public void Ulnar_e_radial_no_mesmo_par_sao_discordantes()
    {
        var padroes = Todos(PadraoDigital.AlcaUlnar);
        padroes[new PosicaoDedo(Mao.Direita, Dedo.Indicador)] = PadraoDigital.AlcaRadial;

        Assert.Equal(1, IndicadoresDermatoglificos.Calcular(padroes).DiscordanciaBilateral);
    }

    [Fact]
    public void Exige_os_dez_dedos()
    {
        var padroes = Todos(PadraoDigital.Arco);
        padroes.Remove(new PosicaoDedo(Mao.Esquerda, Dedo.Minimo));

        Assert.Throws<RegraDeNegocioException>(() => IndicadoresDermatoglificos.Calcular(padroes));
    }
}

public class ArquivoDigitalTestes
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Rejeita_resolucao_abaixo_de_500_ppi()
        => Assert.Throws<RegraDeNegocioException>(() => new ArquivoDigital("s/a.png.enc", HashValido, "PNG", 499, 100, 100));

    [Fact]
    public void Rejeita_hash_invalido()
        => Assert.Throws<RegraDeNegocioException>(() => new ArquivoDigital("s/a.png.enc", "xyz", "PNG", 500, 100, 100));

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("../fora/a.png.enc")]
    public void Rejeita_caminho_fora_do_armazenamento(string caminho)
        => Assert.Throws<RegraDeNegocioException>(() => new ArquivoDigital(caminho, HashValido, "PNG", 500, 100, 100));

    [Fact]
    public void Aceita_arquivo_valido_e_normaliza_hash_e_formato()
    {
        var arquivo = new ArquivoDigital("s/a.png.enc", HashValido.ToUpperInvariant(), "png", 500, 100, 100);

        Assert.Equal(HashValido, arquivo.HashSha256);
        Assert.Equal("PNG", arquivo.Formato);
    }
}
