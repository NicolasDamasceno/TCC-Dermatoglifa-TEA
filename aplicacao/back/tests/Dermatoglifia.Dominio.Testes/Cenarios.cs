using System.Security.Cryptography;
using System.Text;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.ObjetosDeValor;
using Dermatoglifia.Dominio.Servicos;

namespace Dermatoglifia.Dominio.Testes;

/// <summary>Dados sintéticos para os testes — nenhum dado real (RNF08).</summary>
internal static class Cenarios
{
    public static readonly DateTimeOffset Agora = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    public static readonly Guid MedicoId = Guid.CreateVersion7();
    public static readonly Guid AdminId = Guid.CreateVersion7();

    public static readonly DadosResponsavel Responsavel = new("Responsável Sintético", "MAE", "(86) 90000-0000");

    public static Paciente PacienteMenor(Guid? medicoId = null) => Paciente.Cadastrar(
        medicoId ?? MedicoId, SexoBiologico.Masculino, "Paciente Sintético",
        new DateOnly(2022, 3, 14), Responsavel, Agora);

    public static Paciente PacienteComConsentimento(Guid? medicoId = null)
    {
        var paciente = PacienteMenor(medicoId);
        paciente.RegistrarConsentimento(TipoTermo.Tcle, "CEP-v1.0", new DateOnly(2026, 11, 1),
            autorizaAnalise: true, cienteUsoIA: true, autorizaUsoTreino: false, MedicoId, Agora);
        return paciente;
    }

    public static ArquivoDigital Arquivo(PosicaoDedo posicao, string sufixo = "")
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(posicao + sufixo)));
        return new ArquivoDigital($"sessao/{posicao.NomeArquivo}{sufixo}.png.enc", hash, "PNG", 500, 400, 400);
    }

    public static SessaoColeta SessaoIniciada(Paciente? paciente = null) => SessaoColeta.Iniciar(
        paciente ?? PacienteComConsentimento(), MedicoId, MetodoCaptura.LeitorOptico, TipoImpressao.Rolada,
        "Leitor sintético", Agora);

    public static SessaoColeta SessaoCompleta()
    {
        var sessao = SessaoIniciada();
        foreach (var posicao in PosicaoDedo.OrdemColeta)
        {
            sessao.RegistrarDigital(posicao, Arquivo(posicao), Agora);
        }

        sessao.Concluir(Agora);
        return sessao;
    }

    public static ModeloIA ModeloAtivo(bool escoreValidado = false)
    {
        var modelo = ModeloIA.Registrar("2026-11-15_padroes_v1", "CNN 2 blocos", OrigemDados.Sintetico,
            escoreValidado, new string('a', 64), "{}", new DateOnly(2026, 11, 15));
        modelo.Ativar(Agora);
        return modelo;
    }

    /// <summary>Distribuição de probabilidades válida com a classe indicada como mais provável.</summary>
    public static Dictionary<ClasseImagem, decimal> Probabilidades(ClasseImagem principal)
        => Enum.GetValues<ClasseImagem>().ToDictionary(c => c, c => c == principal ? 0.8m : 0.05m);

    /// <summary>
    /// Resposta consistente da IA em que todos os dedos têm a mesma classe de
    /// imagem, exceto os informados em <paramref name="excecoes"/>.
    /// </summary>
    public static List<ResultadoDedoRecebido> RespostaConsistente(
        ClasseImagem classePadrao = ClasseImagem.Verticilo,
        Dictionary<PosicaoDedo, ClasseImagem>? excecoes = null)
    {
        var conversao = TabelaConversaoAlca.Impressao;
        return PosicaoDedo.OrdemColeta.Select(posicao =>
        {
            var classe = excecoes is not null && excecoes.TryGetValue(posicao, out var c) ? c : classePadrao;
            return new ResultadoDedoRecebido(posicao, classe, conversao.Converter(classe, posicao.Mao), 0.8m,
                Probabilidades(classe), AlertaQualidade: false);
        }).ToList();
    }

    public static IndicadoresDermatoglificos IndicadoresDe(IEnumerable<ResultadoDedoRecebido> resultados)
        => IndicadoresDermatoglificos.Calcular(resultados.ToDictionary(r => r.Posicao, r => r.PadraoInformado));
}
