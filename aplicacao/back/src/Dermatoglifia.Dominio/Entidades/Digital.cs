using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.ObjetosDeValor;

namespace Dermatoglifia.Dominio.Entidades;

/// <summary>
/// Uma das 10 digitais de uma sessão. Existe um único registro por mão+dedo;
/// refazer a captura substitui o registro (doc 05).
/// </summary>
public sealed class Digital : Entidade
{
    public Guid SessaoColetaId { get; private set; }
    public Mao Mao { get; private set; }
    public Dedo Dedo { get; private set; }
    public bool Coletavel { get; private set; }
    public string? MotivoNaoColetavel { get; private set; }
    public string? CaminhoArquivo { get; private set; }
    public string? HashSha256 { get; private set; }
    public string? Formato { get; private set; }
    public int? ResolucaoDpi { get; private set; }
    public int? LarguraPx { get; private set; }
    public int? AlturaPx { get; private set; }
    public int? Nfiq2 { get; private set; }
    public DateTimeOffset CapturadaEm { get; private set; }

    public PosicaoDedo Posicao => new(Mao, Dedo);

    private Digital() { } // EF Core

    internal static Digital ComArquivo(Guid sessaoId, PosicaoDedo posicao, ArquivoDigital arquivo, DateTimeOffset agora)
    {
        var digital = new Digital { SessaoColetaId = sessaoId, Mao = posicao.Mao, Dedo = posicao.Dedo };
        digital.DefinirArquivo(arquivo, agora);
        return digital;
    }

    internal static Digital NaoColetavel(Guid sessaoId, PosicaoDedo posicao, string motivo, DateTimeOffset agora)
    {
        var digital = new Digital { SessaoColetaId = sessaoId, Mao = posicao.Mao, Dedo = posicao.Dedo };
        digital.DefinirNaoColetavel(motivo, agora);
        return digital;
    }

    internal void DefinirArquivo(ArquivoDigital arquivo, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        Coletavel = true;
        MotivoNaoColetavel = null;
        CaminhoArquivo = arquivo.CaminhoArquivo;
        HashSha256 = arquivo.HashSha256;
        Formato = arquivo.Formato;
        ResolucaoDpi = arquivo.ResolucaoDpi;
        LarguraPx = arquivo.LarguraPx;
        AlturaPx = arquivo.AlturaPx;
        Nfiq2 = null;
        CapturadaEm = agora;
    }

    internal void DefinirNaoColetavel(string motivo, DateTimeOffset agora)
    {
        Coletavel = false;
        MotivoNaoColetavel = Regra.TextoObrigatorio(motivo, "Motivo", 200);
        CaminhoArquivo = null;
        HashSha256 = null;
        Formato = null;
        ResolucaoDpi = null;
        LarguraPx = null;
        AlturaPx = null;
        Nfiq2 = null;
        CapturadaEm = agora;
    }
}
