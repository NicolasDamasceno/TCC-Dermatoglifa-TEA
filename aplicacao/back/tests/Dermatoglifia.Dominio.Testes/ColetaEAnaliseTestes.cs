using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.Excecoes;
using Dermatoglifia.Dominio.ObjetosDeValor;
using Dermatoglifia.Dominio.Servicos;
using static Dermatoglifia.Dominio.Testes.Cenarios;

namespace Dermatoglifia.Dominio.Testes;

public class SessaoColetaTestes
{
    [Fact]
    public void Nao_inicia_sem_consentimento()
        => Assert.Throws<RegraDeNegocioException>(() => SessaoIniciada(PacienteMenor()));

    [Fact]
    public void Nao_inicia_por_medico_que_nao_e_o_responsavel()
        => Assert.Throws<RegraDeNegocioException>(() => SessaoColeta.Iniciar(
            PacienteComConsentimento(), Guid.CreateVersion7(), MetodoCaptura.LeitorOptico, TipoImpressao.Rolada, null, Agora));

    [Fact]
    public void Registra_idade_em_meses_na_abertura()
        => Assert.Equal(55, SessaoIniciada().IdadeMeses); // 14/03/2022 → 03/11/2026

    [Fact]
    public void Dez_dedos_coletados_concluem_como_completa()
    {
        var sessao = SessaoCompleta();

        Assert.Equal(StatusSessao.Completa, sessao.Status);
        Assert.Equal(10, sessao.Digitais.Count);
        Assert.Empty(sessao.DedosPendentes);
    }

    [Fact]
    public void Nao_conclui_com_dedos_pendentes()
    {
        var sessao = SessaoIniciada();
        sessao.RegistrarDigital(PosicaoDedo.OrdemColeta[0], Arquivo(PosicaoDedo.OrdemColeta[0]), Agora);

        Assert.Throws<RegraDeNegocioException>(() => sessao.Concluir(Agora));
        Assert.Equal(9, sessao.DedosPendentes.Count());
    }

    [Fact]
    public void Dedo_nao_coletavel_conclui_como_incompleta()
    {
        var sessao = SessaoIniciada();
        var naoColetavel = new PosicaoDedo(Mao.Esquerda, Dedo.Anelar);
        foreach (var posicao in PosicaoDedo.OrdemColeta.Where(p => p != naoColetavel))
        {
            sessao.RegistrarDigital(posicao, Arquivo(posicao), Agora);
        }

        sessao.MarcarNaoColetavel(naoColetavel, "Lesão no dedo", Agora);

        Assert.Equal(StatusSessao.Incompleta, sessao.Concluir(Agora));
    }

    [Fact]
    public void Refazer_um_dedo_substitui_e_devolve_o_arquivo_anterior()
    {
        var sessao = SessaoIniciada();
        var posicao = PosicaoDedo.OrdemColeta[3];
        sessao.RegistrarDigital(posicao, Arquivo(posicao), Agora);

        var anterior = sessao.RegistrarDigital(posicao, Arquivo(posicao, "-refeito"), Agora);

        Assert.Equal($"sessao/{posicao.NomeArquivo}.png.enc", anterior);
        Assert.Single(sessao.Digitais);
        Assert.Equal($"sessao/{posicao.NomeArquivo}-refeito.png.enc", sessao.Digitais.Single().CaminhoArquivo);
    }

    [Fact]
    public void Sessao_concluida_nao_aceita_novas_digitais()
    {
        var sessao = SessaoCompleta();
        var posicao = PosicaoDedo.OrdemColeta[0];

        Assert.Throws<RegraDeNegocioException>(() => sessao.RegistrarDigital(posicao, Arquivo(posicao), Agora));
        Assert.Throws<RegraDeNegocioException>(() => sessao.Cancelar(Agora));
    }
}

public class AnaliseTestes
{
    private static (SessaoColeta Sessao, Analise Analise) AnaliseEmProcessamento()
    {
        var sessao = SessaoCompleta();
        var analise = Analise.Solicitar(sessao, "v1", Agora);
        analise.IniciarProcessamento();
        return (sessao, analise);
    }

    [Fact]
    public void So_analisa_sessao_completa()
        => Assert.Throws<RegraDeNegocioException>(() => Analise.Solicitar(SessaoIniciada(), "v1", Agora));

    [Fact]
    public void Resposta_consistente_conclui_com_indicadores_recalculados()
    {
        var (sessao, analise) = AnaliseEmProcessamento();
        var resposta = RespostaConsistente(ClasseImagem.AlcaDireita, new()
        {
            [new PosicaoDedo(Mao.Esquerda, Dedo.Indicador)] = ClasseImagem.Verticilo,
        });

        analise.Concluir(sessao, ModeloAtivo(), TabelaConversaoAlca.Impressao, resposta, IndicadoresDe(resposta), null, Agora);

        Assert.Equal(StatusAnalise.Concluida, analise.Status);
        Assert.Equal(10, analise.Resultados.Count);
        Assert.NotNull(analise.Indicadores);
        // Alça para a direita: ulnar na mão direita (5), radial na esquerda (4, um virou verticilo)
        Assert.Equal(5, analise.Indicadores!.QtdAlcasUlnares);
        Assert.Equal(4, analise.Indicadores.QtdAlcasRadiais);
        Assert.Equal(1, analise.Indicadores.QtdVerticilos);
        Assert.Equal(4, analise.Indicadores.DiscordanciaBilateral);
        Assert.Equal(5, analise.Indicadores.DiscordanciaBilateralComPolegar);
    }

    [Fact]
    public void Padrao_incoerente_com_a_mao_e_rejeitado()
    {
        var (sessao, analise) = AnaliseEmProcessamento();
        var resposta = RespostaConsistente(ClasseImagem.AlcaDireita);
        resposta[0] = resposta[0] with { PadraoInformado = PadraoDigital.AlcaRadial }; // mão direita: deveria ser ulnar

        Assert.Throws<RegraDeNegocioException>(() => analise.Concluir(sessao, ModeloAtivo(),
            TabelaConversaoAlca.Impressao, resposta, IndicadoresDe(RespostaConsistente(ClasseImagem.AlcaDireita)), null, Agora));
        Assert.Equal(StatusAnalise.Processando, analise.Status);
    }

    [Fact]
    public void Indicadores_divergentes_sao_rejeitados()
    {
        var (sessao, analise) = AnaliseEmProcessamento();
        var resposta = RespostaConsistente();
        var adulterados = IndicadoresDe(resposta) with { DiscordanciaBilateral = 3 };

        Assert.Throws<RegraDeNegocioException>(() => analise.Concluir(sessao, ModeloAtivo(),
            TabelaConversaoAlca.Impressao, resposta, adulterados, null, Agora));
    }

    [Fact]
    public void Faltando_um_dedo_e_rejeitado()
    {
        var (sessao, analise) = AnaliseEmProcessamento();
        var resposta = RespostaConsistente();
        resposta.RemoveAt(9);
        resposta.Add(resposta[0]); // dedo repetido no lugar do que faltou

        Assert.Throws<RegraDeNegocioException>(() => analise.Concluir(sessao, ModeloAtivo(),
            TabelaConversaoAlca.Impressao, resposta, IndicadoresDe(RespostaConsistente()), null, Agora));
    }

    [Fact]
    public void Probabilidades_que_nao_somam_um_sao_rejeitadas()
    {
        var (sessao, analise) = AnaliseEmProcessamento();
        var resposta = RespostaConsistente();
        var probabilidades = Probabilidades(ClasseImagem.Verticilo);
        probabilidades[ClasseImagem.Arco] = 0.5m;
        resposta[2] = resposta[2] with { Probabilidades = probabilidades };

        Assert.Throws<RegraDeNegocioException>(() => analise.Concluir(sessao, ModeloAtivo(),
            TabelaConversaoAlca.Impressao, resposta, IndicadoresDe(resposta), null, Agora));
    }

    [Fact]
    public void Modelo_inativo_e_rejeitado()
    {
        var (sessao, analise) = AnaliseEmProcessamento();
        var modelo = ModeloAtivo();
        modelo.Desativar();
        var resposta = RespostaConsistente();

        Assert.Throws<RegraDeNegocioException>(() => analise.Concluir(sessao, modelo,
            TabelaConversaoAlca.Impressao, resposta, IndicadoresDe(resposta), null, Agora));
    }

    [Fact]
    public void Escore_e_descartado_enquanto_a_etapa_2_nao_for_validada()
    {
        var (sessao, analise) = AnaliseEmProcessamento();
        var resposta = RespostaConsistente();

        analise.Concluir(sessao, ModeloAtivo(escoreValidado: false), TabelaConversaoAlca.Impressao,
            resposta, IndicadoresDe(resposta), 0.73m, Agora);

        Assert.Null(analise.EscoreRisco);
    }

    [Fact]
    public void Escore_e_aceito_quando_a_etapa_2_foi_validada()
    {
        var (sessao, analise) = AnaliseEmProcessamento();
        var resposta = RespostaConsistente();

        analise.Concluir(sessao, ModeloAtivo(escoreValidado: true), TabelaConversaoAlca.Impressao,
            resposta, IndicadoresDe(resposta), 0.73m, Agora);

        Assert.Equal(0.73m, analise.EscoreRisco);
    }

    [Fact]
    public void Erro_permite_nova_tentativa()
    {
        var (_, analise) = AnaliseEmProcessamento();

        analise.Falhar("Serviço de IA indisponível");
        Assert.Equal(StatusAnalise.Erro, analise.Status);

        analise.IniciarProcessamento();
        Assert.Equal(StatusAnalise.Processando, analise.Status);
        Assert.Null(analise.MensagemErro);
    }

    [Fact]
    public void Registro_no_prontuario_so_depois_de_concluida_e_uma_vez()
    {
        var (sessao, analise) = AnaliseEmProcessamento();
        Assert.Throws<RegraDeNegocioException>(() => analise.ConfirmarRegistroProntuario(Agora));

        var resposta = RespostaConsistente();
        analise.Concluir(sessao, ModeloAtivo(), TabelaConversaoAlca.Impressao, resposta, IndicadoresDe(resposta), null, Agora);
        analise.ConfirmarRegistroProntuario(Agora);

        Assert.Equal(Agora, analise.RegistradoProntuarioEm);
        Assert.Throws<RegraDeNegocioException>(() => analise.ConfirmarRegistroProntuario(Agora));
    }
}
