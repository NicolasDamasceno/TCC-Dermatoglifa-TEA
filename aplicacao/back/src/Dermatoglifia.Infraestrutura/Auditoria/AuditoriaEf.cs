using System.Text.Json;
using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Infraestrutura.Persistencia;

namespace Dermatoglifia.Infraestrutura.Auditoria;

/// <summary>
/// Adiciona o registro ao mesmo <see cref="AppDbContext"/> da operação: ele é
/// gravado junto, ou não é gravado (RNF07).
/// </summary>
internal sealed class AuditoriaEf(AppDbContext contexto, IUsuarioAtual usuarioAtual, IRelogio relogio) : IAuditoria
{
    /// <summary>
    /// Únicas chaves aceitas em "detalhes". Impede que nomes, contatos, imagens ou
    /// senhas cheguem ao log por descuido (doc 08, seção 8).
    /// </summary>
    private static readonly HashSet<string> ChavesPermitidas =
        ["status", "motivo", "resultado", "versao_modelo", "mao", "dedo", "tipo", "quantidade"];

    public void Registrar(
        string acao,
        string entidade,
        Guid? entidadeId,
        IReadOnlyDictionary<string, string>? detalhes = null,
        Guid? usuarioId = null)
    {
        var permitidos = detalhes?
            .Where(d => ChavesPermitidas.Contains(d.Key))
            .ToDictionary(d => d.Key, d => d.Value);

        contexto.Auditoria.Add(RegistroAuditoria.Criar(
            usuarioId ?? usuarioAtual.Id,
            acao,
            entidade,
            entidadeId,
            usuarioAtual.Ip,
            permitidos is { Count: > 0 } ? JsonSerializer.Serialize(permitidos) : null,
            relogio.Agora));
    }
}
