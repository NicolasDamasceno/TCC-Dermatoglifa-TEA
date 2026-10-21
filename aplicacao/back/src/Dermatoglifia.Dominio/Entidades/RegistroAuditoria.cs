using System.Net;

namespace Dermatoglifia.Dominio.Entidades;

/// <summary>
/// Linha da trilha de auditoria — somente inserção (RNF07). <see cref="Detalhes"/>
/// é um JSON que nunca contém imagem, nome de paciente ou senha.
/// </summary>
public sealed class RegistroAuditoria
{
    public long Id { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public string Acao { get; private set; } = null!;
    public string Entidade { get; private set; } = null!;
    public Guid? EntidadeId { get; private set; }
    public IPAddress? Ip { get; private set; }
    public string? Detalhes { get; private set; }
    public DateTimeOffset OcorridoEm { get; private set; }

    private RegistroAuditoria() { } // EF Core

    public static RegistroAuditoria Criar(
        Guid? usuarioId,
        string acao,
        string entidade,
        Guid? entidadeId,
        IPAddress? ip,
        string? detalhesJson,
        DateTimeOffset agora)
        => new()
        {
            UsuarioId = usuarioId,
            Acao = Regra.TextoObrigatorio(acao, "Ação", 60),
            Entidade = Regra.TextoObrigatorio(entidade, "Entidade", 60),
            EntidadeId = entidadeId,
            Ip = ip,
            Detalhes = detalhesJson,
            OcorridoEm = agora,
        };
}
