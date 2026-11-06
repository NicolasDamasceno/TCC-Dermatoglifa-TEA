using Dermatoglifia.Dominio.Enums;

namespace Dermatoglifia.Dominio.Entidades;

/// <summary>
/// Participante pseudônimo: não guarda nome, nascimento nem contato — esses
/// dados ficam em <see cref="IdentificacaoPaciente"/> (doc 05).
/// </summary>
public sealed class Paciente : Entidade
{
    private readonly List<Consentimento> _consentimentos = [];

    public Guid MedicoResponsavelId { get; private set; }
    public SexoBiologico Sexo { get; private set; }
    public SituacaoDiagnostica Situacao { get; private set; }
    public string? FonteDiagnostico { get; private set; }

    /// <summary>Dia da avaliação clínica que definiu a situação (intervalo até a coleta; QUADAS-2).</summary>
    public DateOnly? DiagnosticoAvaliadoEm { get; private set; }
    public DateTimeOffset? SituacaoAtualizadaEm { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? AnonimizadoEm { get; private set; }

    public IdentificacaoPaciente? Identificacao { get; private set; }
    public IReadOnlyCollection<Consentimento> Consentimentos => _consentimentos.AsReadOnly();

    public bool Anonimizado => AnonimizadoEm is not null;

    private Paciente() { } // EF Core

    /// <summary>Cadastra o paciente e sua identificação separada (UC04).</summary>
    public static Paciente Cadastrar(
        Guid medicoResponsavelId,
        SexoBiologico sexo,
        string nome,
        DateOnly dataNascimento,
        DadosResponsavel? responsavel,
        DateTimeOffset agora)
    {
        Regra.Exigir(medicoResponsavelId != Guid.Empty, "Médico responsável inválido.");

        var paciente = new Paciente
        {
            MedicoResponsavelId = medicoResponsavelId,
            Sexo = sexo,
            Situacao = SituacaoDiagnostica.Suspeita,
            CriadoEm = agora,
        };

        paciente.Identificacao = IdentificacaoPaciente.Criar(
            paciente.Id, nome, dataNascimento, responsavel, DateOnly.FromDateTime(agora.UtcDateTime));

        return paciente;
    }

    /// <summary>Registra TCLE ou TALE (UC05).</summary>
    public Consentimento RegistrarConsentimento(
        TipoTermo tipo,
        string versaoTermo,
        DateOnly assinadoEm,
        bool autorizaAnalise,
        bool cienteUsoIA,
        bool autorizaUsoTreino,
        Guid registradoPor,
        DateTimeOffset agora)
    {
        GarantirNaoAnonimizado();

        var consentimento = Consentimento.Criar(
            Id, tipo, versaoTermo, assinadoEm, autorizaAnalise, cienteUsoIA, autorizaUsoTreino,
            registradoPor, DateOnly.FromDateTime(agora.UtcDateTime));

        _consentimentos.Add(consentimento);
        return consentimento;
    }

    public void RevogarConsentimento(Guid consentimentoId, DateTimeOffset agora)
    {
        var consentimento = _consentimentos.SingleOrDefault(c => c.Id == consentimentoId);
        Regra.Exigir(consentimento is not null, "Consentimento não encontrado para este paciente.");
        consentimento!.Revogar(agora);
    }

    /// <summary>Há TCLE vigente com autorização da análise e ciência do uso de IA (RN03).</summary>
    public bool PodeColetar => !Anonimizado && _consentimentos.Any(c => c.LiberaColeta);

    /// <summary>Digitais só podem ir para treino com autorização específica vigente (RN07).</summary>
    public bool AutorizaUsoEmTreino =>
        !Anonimizado && _consentimentos.Any(c => c.Tipo == TipoTermo.Tcle && c.EstaVigente && c.AutorizaUsoTreino);

    /// <summary>
    /// Atualiza a situação após o diagnóstico clínico, feito fora do sistema (UC13; ADR-0009).
    /// A situação é o padrão de referência de qualquer avaliação futura do sistema; por
    /// isso exige fonte, data da avaliação e a declaração de que o diagnóstico foi
    /// fechado sem o resultado deste sistema (QUADAS-2, domínios 3 e 4). Voltar para
    /// <see cref="SituacaoDiagnostica.Suspeita"/> corrige um registro feito por engano
    /// e apaga fonte e data.
    /// </summary>
    public void AtualizarSituacao(
        SituacaoDiagnostica situacao,
        string? fonte,
        DateOnly? avaliadoEm,
        bool independenteDoSistema,
        DateTimeOffset agora)
    {
        GarantirNaoAnonimizado();

        if (situacao == SituacaoDiagnostica.Suspeita)
        {
            Regra.Exigir(string.IsNullOrWhiteSpace(fonte) && avaliadoEm is null,
                "A situação \"suspeita\" não tem fonte nem data de avaliação.");
            Regra.Exigir(Situacao != SituacaoDiagnostica.Suspeita, "O paciente já está como suspeita.");
            FonteDiagnostico = null;
            DiagnosticoAvaliadoEm = null;
        }
        else
        {
            var textoFonte = Regra.TextoObrigatorio(fonte, "Fonte do diagnóstico", 200);
            Regra.Exigir(avaliadoEm is not null, "Data da avaliação clínica é obrigatória.");
            Regra.Exigir(avaliadoEm <= DateOnly.FromDateTime(agora.UtcDateTime),
                "A data da avaliação clínica não pode estar no futuro.");
            Regra.Exigir(Identificacao is null || avaliadoEm >= Identificacao.DataNascimento,
                "A data da avaliação clínica é anterior ao nascimento.");
            Regra.Exigir(independenteDoSistema,
                "Confirme que o diagnóstico foi fechado sem considerar o resultado deste sistema.");
            Regra.Exigir(
                situacao != Situacao || textoFonte != FonteDiagnostico || avaliadoEm != DiagnosticoAvaliadoEm,
                "Nada mudou: a situação, a fonte e a data já são essas.");
            FonteDiagnostico = textoFonte;
            DiagnosticoAvaliadoEm = avaliadoEm;
        }

        Situacao = situacao;
        SituacaoAtualizadaEm = agora;
    }

    /// <summary>Corrige a identificação a pedido do titular (UC12; ADR-0010).</summary>
    public void CorrigirIdentificacao(string nome, DateOnly dataNascimento, DadosResponsavel? responsavel, DateTimeOffset agora)
    {
        GarantirNaoAnonimizado();
        Regra.Exigir(DiagnosticoAvaliadoEm is null || DiagnosticoAvaliadoEm >= dataNascimento,
            "A nova data de nascimento é posterior à data da avaliação clínica registrada.");
        Identificacao!.Corrigir(nome, dataNascimento, responsavel, DateOnly.FromDateTime(agora.UtcDateTime));
    }

    /// <summary>
    /// Atende pedido de eliminação do titular (UC12): a identificação deixa de
    /// existir; ficam apenas dados pseudônimos agregados. As imagens e os textos
    /// livres são apagados pelas sessões e análises (RN08).
    /// </summary>
    public void Anonimizar(DateTimeOffset agora)
    {
        GarantirNaoAnonimizado();
        Identificacao = null;
        AnonimizadoEm = agora;
    }

    private void GarantirNaoAnonimizado()
        => Regra.Exigir(!Anonimizado, "Os dados deste paciente foram eliminados a pedido do titular.");
}
