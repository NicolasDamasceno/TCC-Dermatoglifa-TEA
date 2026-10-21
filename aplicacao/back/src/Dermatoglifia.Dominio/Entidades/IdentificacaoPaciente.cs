namespace Dermatoglifia.Dominio.Entidades;

/// <summary>
/// Dados que identificam a pessoa. Ficam separados do <see cref="Paciente"/>
/// pseudônimo (schema próprio no banco) para que biometria e resultados só
/// conheçam o identificador (LGPD art. 13, §4º; doc 05).
/// </summary>
public sealed class IdentificacaoPaciente
{
    public const int MaioridadeAnos = 18;

    public Guid PacienteId { get; private set; }
    public string Nome { get; private set; } = null!;
    public DateOnly DataNascimento { get; private set; }
    public string? ResponsavelNome { get; private set; }
    public string? ResponsavelParentesco { get; private set; }
    public string? ResponsavelContato { get; private set; }

    private IdentificacaoPaciente() { } // EF Core

    internal static IdentificacaoPaciente Criar(
        Guid pacienteId,
        string nome,
        DateOnly dataNascimento,
        DadosResponsavel? responsavel,
        DateOnly hoje)
    {
        Regra.Exigir(dataNascimento <= hoje, "A data de nascimento não pode estar no futuro.");
        Regra.Exigir(dataNascimento > hoje.AddYears(-120), "Data de nascimento inválida.");

        var identificacao = new IdentificacaoPaciente
        {
            PacienteId = pacienteId,
            Nome = Regra.TextoObrigatorio(nome, "Nome", 150),
            DataNascimento = dataNascimento,
            ResponsavelNome = Regra.TextoOpcional(responsavel?.Nome, "Nome do responsável", 150),
            ResponsavelParentesco = Regra.TextoOpcional(responsavel?.Parentesco, "Parentesco do responsável", 50),
            ResponsavelContato = Regra.TextoOpcional(responsavel?.Contato, "Contato do responsável", 100),
        };

        if (identificacao.EhMenorDeIdade(hoje))
        {
            Regra.Exigir(
                identificacao.ResponsavelNome is not null
                && identificacao.ResponsavelParentesco is not null
                && identificacao.ResponsavelContato is not null,
                "Para menores de 18 anos, nome, parentesco e contato do responsável legal são obrigatórios.");
        }

        return identificacao;
    }

    public bool EhMenorDeIdade(DateOnly hoje) => DataNascimento.AddYears(MaioridadeAnos) > hoje;

    /// <summary>Idade em meses completos na data informada.</summary>
    public int IdadeEmMeses(DateOnly data)
    {
        Regra.Exigir(data >= DataNascimento, "Data anterior ao nascimento.");
        var meses = ((data.Year - DataNascimento.Year) * 12) + data.Month - DataNascimento.Month;
        if (data.Day < DataNascimento.Day)
        {
            meses--;
        }

        return meses;
    }
}

/// <summary>Dados do responsável legal informados no cadastro.</summary>
public sealed record DadosResponsavel(string? Nome, string? Parentesco, string? Contato);
