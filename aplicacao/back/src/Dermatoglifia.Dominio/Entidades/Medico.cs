namespace Dermatoglifia.Dominio.Entidades;

/// <summary>
/// Dados profissionais de um usuário com papel MEDICO (relação 1-para-1 com a
/// conta de usuário, que vive na Infraestrutura por causa do ASP.NET Core
/// Identity — ADR-0006). A chave é o id do usuário.
/// </summary>
public sealed class Medico
{
    private static readonly HashSet<string> UfsValidas =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ];

    public Guid UsuarioId { get; private set; }
    public string Crm { get; private set; } = null!;
    public string UfCrm { get; private set; } = null!;
    public string Especialidade { get; private set; } = null!;
    public string? VinculoProtocolo { get; private set; }
    public Guid? AprovadoPor { get; private set; }
    public DateTimeOffset? AprovadoEm { get; private set; }
    public string? MotivoRecusa { get; private set; }

    public bool Aprovado => AprovadoEm is not null;
    public bool Recusado => MotivoRecusa is not null;
    public bool Pendente => !Aprovado && !Recusado;

    private Medico() { } // EF Core

    public static Medico Solicitar(Guid usuarioId, string crm, string ufCrm, string especialidade)
    {
        Regra.Exigir(usuarioId != Guid.Empty, "Usuário inválido.");

        var numeroCrm = Regra.TextoObrigatorio(crm, "CRM", 10);
        Regra.Exigir(numeroCrm.All(char.IsAsciiDigit), "O CRM deve conter apenas números.");

        var uf = Regra.TextoObrigatorio(ufCrm, "UF do CRM", 2).ToUpperInvariant();
        Regra.Exigir(UfsValidas.Contains(uf), "UF do CRM inválida.");

        return new Medico
        {
            UsuarioId = usuarioId,
            Crm = numeroCrm,
            UfCrm = uf,
            Especialidade = Regra.TextoObrigatorio(especialidade, "Especialidade", 100),
        };
    }

    /// <summary>
    /// Aprova o cadastro. Só médicos vinculados ao protocolo aprovado pelo CEP
    /// podem usar o sistema (RN01; RDC 751/2022, art. 2º, §8º).
    /// </summary>
    public void Aprovar(Guid adminId, string vinculoProtocolo, DateTimeOffset agora)
    {
        Regra.Exigir(Pendente, "Este cadastro já foi analisado.");
        Regra.Exigir(adminId != Guid.Empty, "Administrador inválido.");

        VinculoProtocolo = Regra.TextoObrigatorio(vinculoProtocolo, "Vínculo com o protocolo de pesquisa", 200);
        AprovadoPor = adminId;
        AprovadoEm = agora;
    }

    public void Recusar(string motivo)
    {
        Regra.Exigir(Pendente, "Este cadastro já foi analisado.");
        MotivoRecusa = Regra.TextoObrigatorio(motivo, "Motivo da recusa", 300);
    }
}
