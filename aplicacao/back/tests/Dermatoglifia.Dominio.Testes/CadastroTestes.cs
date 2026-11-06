using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.Excecoes;
using static Dermatoglifia.Dominio.Testes.Cenarios;

namespace Dermatoglifia.Dominio.Testes;

public class MedicoTestes
{
    [Theory]
    [InlineData("12A45", "PI")]
    [InlineData("", "PI")]
    [InlineData("12345", "XX")]
    public void Rejeita_crm_ou_uf_invalidos(string crm, string uf)
        => Assert.Throws<RegraDeNegocioException>(() => Medico.Solicitar(Guid.CreateVersion7(), crm, uf, "Pediatria"));

    [Fact]
    public void Aprovacao_exige_vinculo_com_o_protocolo()
    {
        var medico = Medico.Solicitar(Guid.CreateVersion7(), "12345", "pi", "Pediatria");

        Assert.Equal("PI", medico.UfCrm);
        Assert.Throws<RegraDeNegocioException>(() => medico.Aprovar(AdminId, " ", Agora));
        Assert.True(medico.Pendente);
    }

    [Fact]
    public void Cadastro_so_e_analisado_uma_vez()
    {
        var medico = Medico.Solicitar(Guid.CreateVersion7(), "12345", "PI", "Pediatria");
        medico.Aprovar(AdminId, "Equipe do protocolo CAAE 0000", Agora);

        Assert.True(medico.Aprovado);
        Assert.Throws<RegraDeNegocioException>(() => medico.Recusar("Fora do protocolo"));
        Assert.Throws<RegraDeNegocioException>(() => medico.Aprovar(AdminId, "Outro", Agora));
    }
}

public class PacienteTestes
{
    [Fact]
    public void Menor_de_idade_exige_responsavel_completo()
    {
        Assert.Throws<RegraDeNegocioException>(() => Paciente.Cadastrar(MedicoId, SexoBiologico.Feminino,
            "Criança Sintética", new DateOnly(2020, 1, 1), responsavel: null, Agora));

        Assert.Throws<RegraDeNegocioException>(() => Paciente.Cadastrar(MedicoId, SexoBiologico.Feminino,
            "Criança Sintética", new DateOnly(2020, 1, 1), new DadosResponsavel("Mãe", "MAE", null), Agora));
    }

    [Fact]
    public void Adulto_dispensa_responsavel()
    {
        var paciente = Paciente.Cadastrar(MedicoId, SexoBiologico.Masculino, "Adulto Sintético",
            new DateOnly(1990, 5, 20), responsavel: null, Agora);

        Assert.NotNull(paciente.Identificacao);
        Assert.False(paciente.Identificacao!.EhMenorDeIdade(DateOnly.FromDateTime(Agora.UtcDateTime)));
        Assert.Equal(SituacaoDiagnostica.Suspeita, paciente.Situacao);
    }

    [Fact]
    public void Rejeita_nascimento_no_futuro()
        => Assert.Throws<RegraDeNegocioException>(() => Paciente.Cadastrar(MedicoId, SexoBiologico.Masculino,
            "Sintético", new DateOnly(2027, 1, 1), Responsavel, Agora));

    [Theory]
    [InlineData(2026, 3, 13, 47)]
    [InlineData(2026, 3, 14, 48)]
    [InlineData(2022, 3, 14, 0)]
    public void Idade_em_meses_completos(int ano, int mes, int dia, int esperado)
    {
        var paciente = PacienteMenor(); // nascido em 14/03/2022

        Assert.Equal(esperado, paciente.Identificacao!.IdadeEmMeses(new DateOnly(ano, mes, dia)));
    }

    [Fact]
    public void Sem_consentimento_nao_pode_coletar()
        => Assert.False(PacienteMenor().PodeColetar);

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Tcle_sem_autorizacao_ou_sem_ciencia_da_IA_nao_libera(bool autorizaAnalise, bool cienteUsoIA)
    {
        var paciente = PacienteMenor();
        paciente.RegistrarConsentimento(TipoTermo.Tcle, "CEP-v1.0", new DateOnly(2026, 11, 1),
            autorizaAnalise, cienteUsoIA, autorizaUsoTreino: true, MedicoId, Agora);

        Assert.False(paciente.PodeColetar);
    }

    [Fact]
    public void Tale_sozinho_nao_libera_coleta()
    {
        var paciente = PacienteMenor();
        paciente.RegistrarConsentimento(TipoTermo.Tale, "CEP-v1.0", new DateOnly(2026, 11, 1),
            autorizaAnalise: true, cienteUsoIA: true, autorizaUsoTreino: false, MedicoId, Agora);

        Assert.False(paciente.PodeColetar);
    }

    [Fact]
    public void Revogacao_bloqueia_coleta_e_uso_em_treino()
    {
        var paciente = PacienteMenor();
        var tcle = paciente.RegistrarConsentimento(TipoTermo.Tcle, "CEP-v1.0", new DateOnly(2026, 11, 1),
            autorizaAnalise: true, cienteUsoIA: true, autorizaUsoTreino: true, MedicoId, Agora);
        Assert.True(paciente.PodeColetar);
        Assert.True(paciente.AutorizaUsoEmTreino);

        paciente.RevogarConsentimento(tcle.Id, Agora);

        Assert.False(paciente.PodeColetar);
        Assert.False(paciente.AutorizaUsoEmTreino);
        Assert.Throws<RegraDeNegocioException>(() => paciente.RevogarConsentimento(tcle.Id, Agora));
    }

    [Fact]
    public void Assinatura_no_futuro_e_rejeitada()
        => Assert.Throws<RegraDeNegocioException>(() => PacienteMenor().RegistrarConsentimento(TipoTermo.Tcle,
            "CEP-v1.0", new DateOnly(2026, 12, 1), true, true, false, MedicoId, Agora));

    private static readonly DateOnly Avaliacao = new(2026, 10, 20);

    [Fact]
    public void Situacao_diagnostica_definida_exige_fonte_data_e_independencia()
    {
        var paciente = PacienteMenor();

        Assert.Throws<RegraDeNegocioException>(() =>
            paciente.AtualizarSituacao(SituacaoDiagnostica.TeaConfirmado, null, Avaliacao, true, Agora));
        Assert.Throws<RegraDeNegocioException>(() =>
            paciente.AtualizarSituacao(SituacaoDiagnostica.TeaConfirmado, "Avaliação multiprofissional", null, true, Agora));
        Assert.Throws<RegraDeNegocioException>(() =>
            paciente.AtualizarSituacao(SituacaoDiagnostica.TeaConfirmado, "Avaliação multiprofissional", Avaliacao, false, Agora));

        paciente.AtualizarSituacao(SituacaoDiagnostica.TeaConfirmado, "  Avaliação multiprofissional ", Avaliacao, true, Agora);

        Assert.Equal(SituacaoDiagnostica.TeaConfirmado, paciente.Situacao);
        Assert.Equal("Avaliação multiprofissional", paciente.FonteDiagnostico);
        Assert.Equal(Avaliacao, paciente.DiagnosticoAvaliadoEm);
        Assert.Equal(Agora, paciente.SituacaoAtualizadaEm);
    }

    [Theory]
    [InlineData(2026, 11, 4)] // depois de "agora"
    [InlineData(2022, 3, 13)] // antes do nascimento
    public void Data_da_avaliacao_fora_do_intervalo_e_rejeitada(int ano, int mes, int dia)
        => Assert.Throws<RegraDeNegocioException>(() => PacienteMenor().AtualizarSituacao(
            SituacaoDiagnostica.TeaDescartado, "Avaliação", new DateOnly(ano, mes, dia), true, Agora));

    [Fact]
    public void Voltar_para_suspeita_apaga_fonte_e_data()
    {
        var paciente = PacienteMenor();
        Assert.Throws<RegraDeNegocioException>(() =>
            paciente.AtualizarSituacao(SituacaoDiagnostica.Suspeita, null, null, false, Agora));
        paciente.AtualizarSituacao(SituacaoDiagnostica.TeaDescartado, "Avaliação", Avaliacao, true, Agora);

        Assert.Throws<RegraDeNegocioException>(() =>
            paciente.AtualizarSituacao(SituacaoDiagnostica.Suspeita, "Avaliação", null, false, Agora));
        paciente.AtualizarSituacao(SituacaoDiagnostica.Suspeita, null, null, false, Agora);

        Assert.Equal(SituacaoDiagnostica.Suspeita, paciente.Situacao);
        Assert.Null(paciente.FonteDiagnostico);
        Assert.Null(paciente.DiagnosticoAvaliadoEm);
    }

    [Fact]
    public void Atualizacao_sem_mudanca_e_rejeitada_mas_correcao_da_fonte_e_aceita()
    {
        var paciente = PacienteMenor();
        paciente.AtualizarSituacao(SituacaoDiagnostica.Controle, "Grupo controle do protocolo", Avaliacao, true, Agora);

        Assert.Throws<RegraDeNegocioException>(() =>
            paciente.AtualizarSituacao(SituacaoDiagnostica.Controle, "Grupo controle do protocolo", Avaliacao, true, Agora));

        paciente.AtualizarSituacao(SituacaoDiagnostica.Controle, "Grupo controle do protocolo (corrigido)", Avaliacao, true, Agora);
        Assert.Equal("Grupo controle do protocolo (corrigido)", paciente.FonteDiagnostico);
    }

    [Fact]
    public void Anonimizar_remove_identificacao_e_bloqueia_alteracoes()
    {
        var paciente = PacienteComConsentimento();

        paciente.Anonimizar(Agora);

        Assert.Null(paciente.Identificacao);
        Assert.False(paciente.PodeColetar);
        Assert.Throws<RegraDeNegocioException>(() => paciente.Anonimizar(Agora));
        Assert.Throws<RegraDeNegocioException>(() =>
            paciente.AtualizarSituacao(SituacaoDiagnostica.TeaDescartado, "Avaliação", Avaliacao, true, Agora));
    }

    [Fact]
    public void Correcao_da_identificacao_segue_as_regras_do_cadastro()
    {
        var paciente = PacienteMenor();

        Assert.Throws<RegraDeNegocioException>(() =>
            paciente.CorrigirIdentificacao("Nome Corrigido", new DateOnly(2022, 3, 14), null, Agora)); // menor sem responsável

        paciente.CorrigirIdentificacao(" Nome Corrigido ", new DateOnly(2021, 1, 2), Responsavel, Agora);
        Assert.Equal("Nome Corrigido", paciente.Identificacao!.Nome);
        Assert.Equal(new DateOnly(2021, 1, 2), paciente.Identificacao.DataNascimento);
    }

    [Fact]
    public void Correcao_nao_deixa_o_nascimento_depois_da_avaliacao_clinica()
    {
        var paciente = PacienteMenor();
        paciente.AtualizarSituacao(SituacaoDiagnostica.TeaDescartado, "Avaliação", new DateOnly(2026, 10, 20), true, Agora);

        Assert.Throws<RegraDeNegocioException>(() =>
            paciente.CorrigirIdentificacao("Nome", new DateOnly(2026, 10, 21), Responsavel, Agora));
    }
}
