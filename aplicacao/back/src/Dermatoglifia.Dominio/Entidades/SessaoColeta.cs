using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.ObjetosDeValor;

namespace Dermatoglifia.Dominio.Entidades;

/// <summary>Coleta das 10 digitais de um paciente (UC06; estados no doc 04).</summary>
public sealed class SessaoColeta : Entidade
{
    private readonly List<Digital> _digitais = [];

    public Guid PacienteId { get; private set; }
    public Guid MedicoId { get; private set; }
    public MetodoCaptura Metodo { get; private set; }
    public TipoImpressao TipoImpressao { get; private set; }
    public string? Dispositivo { get; private set; }
    public int IdadeMeses { get; private set; }
    public StatusSessao Status { get; private set; }
    public DateTimeOffset IniciadaEm { get; private set; }
    public DateTimeOffset? ConcluidaEm { get; private set; }
    public string? Observacoes { get; private set; }

    /// <summary>Token de concorrência otimista (coluna xmin do PostgreSQL).</summary>
    public uint Versao { get; private set; }

    public IReadOnlyCollection<Digital> Digitais => _digitais.AsReadOnly();

    private SessaoColeta() { } // EF Core

    public static SessaoColeta Iniciar(
        Paciente paciente,
        Guid medicoId,
        MetodoCaptura metodo,
        TipoImpressao tipoImpressao,
        string? dispositivo,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(paciente);
        Regra.Exigir(paciente.MedicoResponsavelId == medicoId, "Apenas o médico responsável pode coletar as digitais deste paciente.");
        Regra.Exigir(paciente.PodeColetar,
            "A coleta exige TCLE vigente com autorização da análise e ciência do uso de IA.");
        Regra.Exigir(paciente.Identificacao is not null, "Identificação do paciente indisponível.");

        return new SessaoColeta
        {
            PacienteId = paciente.Id,
            MedicoId = medicoId,
            Metodo = metodo,
            TipoImpressao = tipoImpressao,
            Dispositivo = Regra.TextoOpcional(dispositivo, "Dispositivo", 100),
            IdadeMeses = paciente.Identificacao!.IdadeEmMeses(DateOnly.FromDateTime(agora.UtcDateTime)),
            Status = StatusSessao.EmAndamento,
            IniciadaEm = agora,
        };
    }

    /// <summary>
    /// Registra (ou substitui) a imagem de um dedo. Devolve o caminho do arquivo
    /// substituído, para que a camada de aplicação o apague depois do commit.
    /// </summary>
    public string? RegistrarDigital(PosicaoDedo posicao, ArquivoDigital arquivo, DateTimeOffset agora)
    {
        GarantirEmAndamento();
        var existente = Buscar(posicao);
        if (existente is null)
        {
            _digitais.Add(Digital.ComArquivo(Id, posicao, arquivo, agora));
            return null;
        }

        var caminhoAnterior = existente.CaminhoArquivo;
        existente.DefinirArquivo(arquivo, agora);
        return caminhoAnterior;
    }

    /// <summary>Dedo que não pode ser coletado (amputação, lesão etc.) — UC06, 4b.</summary>
    public string? MarcarNaoColetavel(PosicaoDedo posicao, string motivo, DateTimeOffset agora)
    {
        GarantirEmAndamento();
        var existente = Buscar(posicao);
        if (existente is null)
        {
            _digitais.Add(Digital.NaoColetavel(Id, posicao, motivo, agora));
            return null;
        }

        var caminhoAnterior = existente.CaminhoArquivo;
        existente.DefinirNaoColetavel(motivo, agora);
        return caminhoAnterior;
    }

    public IEnumerable<PosicaoDedo> DedosPendentes =>
        PosicaoDedo.OrdemColeta.Where(p => Buscar(p) is null);

    /// <summary>
    /// Conclui a sessão: COMPLETA com os 10 dedos coletados; INCOMPLETA se algum
    /// dedo foi marcado como não coletável — e então não é analisada (RN09).
    /// </summary>
    public StatusSessao Concluir(DateTimeOffset agora)
    {
        GarantirEmAndamento();
        Regra.Exigir(!DedosPendentes.Any(), "Todos os 10 dedos precisam ser coletados ou justificados antes de concluir.");

        Status = _digitais.All(d => d.Coletavel) ? StatusSessao.Completa : StatusSessao.Incompleta;
        ConcluidaEm = agora;
        return Status;
    }

    public void Cancelar(DateTimeOffset agora)
    {
        GarantirEmAndamento();
        Status = StatusSessao.Cancelada;
        ConcluidaEm = agora;
    }

    public void RegistrarObservacoes(string? observacoes)
        => Observacoes = Regra.TextoOpcional(observacoes, "Observações", 1000);

    internal Digital? Buscar(PosicaoDedo posicao)
        => _digitais.SingleOrDefault(d => d.Mao == posicao.Mao && d.Dedo == posicao.Dedo);

    private void GarantirEmAndamento()
        => Regra.Exigir(Status == StatusSessao.EmAndamento, "A sessão de coleta não está em andamento.");
}
