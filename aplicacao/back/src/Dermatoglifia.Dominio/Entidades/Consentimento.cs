using Dermatoglifia.Dominio.Enums;

namespace Dermatoglifia.Dominio.Entidades;

/// <summary>
/// Registro de TCLE (responsável ou paciente adulto) ou TALE (assentimento da
/// criança). Criado apenas por <see cref="Paciente.RegistrarConsentimento"/>.
/// </summary>
public sealed class Consentimento : Entidade
{
    public Guid PacienteId { get; private set; }
    public TipoTermo Tipo { get; private set; }
    public string VersaoTermo { get; private set; } = null!;
    public bool AutorizaAnalise { get; private set; }
    public bool CienteUsoIA { get; private set; }
    public bool AutorizaUsoTreino { get; private set; }
    public DateOnly AssinadoEm { get; private set; }
    public Guid RegistradoPor { get; private set; }
    public DateTimeOffset? RevogadoEm { get; private set; }

    public bool EstaVigente => RevogadoEm is null;

    /// <summary>
    /// TCLE vigente que libera a coleta: autoriza a análise e registra a ciência
    /// do uso de IA (RN03; Res. CFM 2.454/2026).
    /// </summary>
    public bool LiberaColeta => Tipo == TipoTermo.Tcle && EstaVigente && AutorizaAnalise && CienteUsoIA;

    private Consentimento() { } // EF Core

    internal static Consentimento Criar(
        Guid pacienteId,
        TipoTermo tipo,
        string versaoTermo,
        DateOnly assinadoEm,
        bool autorizaAnalise,
        bool cienteUsoIA,
        bool autorizaUsoTreino,
        Guid registradoPor,
        DateOnly hoje)
    {
        Regra.Exigir(assinadoEm <= hoje, "A data de assinatura não pode estar no futuro.");
        Regra.Exigir(registradoPor != Guid.Empty, "Usuário que registra o consentimento é inválido.");

        return new Consentimento
        {
            PacienteId = pacienteId,
            Tipo = tipo,
            VersaoTermo = Regra.TextoObrigatorio(versaoTermo, "Versão do termo", 50),
            AssinadoEm = assinadoEm,
            AutorizaAnalise = autorizaAnalise,
            CienteUsoIA = cienteUsoIA,
            AutorizaUsoTreino = autorizaUsoTreino,
            RegistradoPor = registradoPor,
        };
    }

    internal void Revogar(DateTimeOffset agora)
    {
        Regra.Exigir(EstaVigente, "Este consentimento já foi revogado.");
        RevogadoEm = agora;
    }
}
