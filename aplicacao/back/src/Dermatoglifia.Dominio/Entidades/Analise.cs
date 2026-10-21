using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.ObjetosDeValor;
using Dermatoglifia.Dominio.Servicos;

namespace Dermatoglifia.Dominio.Entidades;

/// <summary>Análise das 10 digitais de uma sessão completa (UC07/UC08; estados no doc 04).</summary>
public sealed class Analise : Entidade
{
    /// <summary>Tolerância para a soma das probabilidades de um dedo.</summary>
    public const decimal ToleranciaSomaProbabilidades = 0.01m;

    private readonly List<ResultadoDigital> _resultados = [];

    public Guid SessaoColetaId { get; private set; }
    public Guid? ModeloIAId { get; private set; }
    public StatusAnalise Status { get; private set; }
    public IndicadoresDermatoglificos? Indicadores { get; private set; }
    public decimal? EscoreRisco { get; private set; }
    public string VersaoAviso { get; private set; } = null!;
    public string? ObservacaoMedico { get; private set; }
    public DateTimeOffset? RegistradoProntuarioEm { get; private set; }
    public string? MensagemErro { get; private set; }
    public DateTimeOffset SolicitadaEm { get; private set; }
    public DateTimeOffset? ConcluidaEm { get; private set; }

    /// <summary>Token de concorrência otimista (coluna xmin do PostgreSQL).</summary>
    public uint Versao { get; private set; }

    public IReadOnlyCollection<ResultadoDigital> Resultados => _resultados.AsReadOnly();

    private Analise() { } // EF Core

    public static Analise Solicitar(SessaoColeta sessao, string versaoAviso, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        Regra.Exigir(sessao.Status == StatusSessao.Completa, "Só sessões completas, com os 10 dedos coletados, são analisadas.");

        return new Analise
        {
            SessaoColetaId = sessao.Id,
            Status = StatusAnalise.Pendente,
            VersaoAviso = Regra.TextoObrigatorio(versaoAviso, "Versão do aviso", 20),
            SolicitadaEm = agora,
        };
    }

    /// <summary>PENDENTE → PROCESSANDO, ou ERRO → PROCESSANDO (nova tentativa).</summary>
    public void IniciarProcessamento()
    {
        Regra.Exigir(Status is StatusAnalise.Pendente or StatusAnalise.Erro,
            "A análise só pode ser processada quando pendente ou após erro.");
        Status = StatusAnalise.Processando;
        MensagemErro = null;
    }

    /// <summary>
    /// Aceita a resposta do Serviço de IA somente se ela for consistente
    /// (doc 08, seção 7). Em caso de inconsistência lança
    /// <see cref="Excecoes.RegraDeNegocioException"/>; a camada de aplicação então
    /// chama <see cref="Falhar"/>.
    /// </summary>
    public void Concluir(
        SessaoColeta sessao,
        ModeloIA modelo,
        TabelaConversaoAlca conversao,
        IReadOnlyList<ResultadoDedoRecebido> recebidos,
        IndicadoresDermatoglificos indicadoresRecebidos,
        decimal? escoreRecebido,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        ArgumentNullException.ThrowIfNull(modelo);
        ArgumentNullException.ThrowIfNull(conversao);
        ArgumentNullException.ThrowIfNull(recebidos);
        ArgumentNullException.ThrowIfNull(indicadoresRecebidos);

        Regra.Exigir(Status == StatusAnalise.Processando, "A análise não está em processamento.");
        Regra.Exigir(sessao.Id == SessaoColetaId, "A sessão não corresponde a esta análise.");
        Regra.Exigir(modelo.Ativo, $"O modelo \"{modelo.Versao}\" não é o modelo ativo.");

        // 1. Exatamente um resultado para cada um dos 10 dedos.
        Regra.Exigir(
            recebidos.Count == PosicaoDedo.TotalDedos
            && recebidos.Select(r => r.Posicao).Distinct().Count() == PosicaoDedo.TotalDedos
            && PosicaoDedo.OrdemColeta.All(p => recebidos.Any(r => r.Posicao == p)),
            "Resposta do modelo inconsistente: é preciso exatamente um resultado por dedo.");

        var padroes = new Dictionary<PosicaoDedo, PadraoDigital>();
        var novos = new List<ResultadoDigital>();
        foreach (var recebido in recebidos)
        {
            // 2. Probabilidades válidas.
            ValidarProbabilidades(recebido);

            // 3. Padrão coerente com a classe da imagem e a mão.
            var padrao = conversao.Converter(recebido.ClasseImagem, recebido.Posicao.Mao);
            Regra.Exigir(padrao == recebido.PadraoInformado,
                $"Resposta do modelo inconsistente: padrão de {recebido.Posicao} não corresponde à classe da imagem e à mão.");

            var digital = sessao.Buscar(recebido.Posicao);
            Regra.Exigir(digital is { Coletavel: true }, $"Não há imagem coletada para {recebido.Posicao}.");

            padroes[recebido.Posicao] = padrao;
            novos.Add(ResultadoDigital.Criar(Id, digital!.Id, recebido, padrao));
        }

        // 4. Indicadores recalculados aqui precisam bater com os recebidos.
        var indicadores = IndicadoresDermatoglificos.Calcular(padroes);
        Regra.Exigir(indicadores == indicadoresRecebidos,
            "Resposta do modelo inconsistente: indicadores diferentes dos calculados a partir dos padrões.");

        // 5. Escore só é aceito se a Etapa 2 do modelo foi validada (RN10).
        decimal? escore = null;
        if (modelo.EscoreValidado && escoreRecebido is not null)
        {
            Regra.Exigir(escoreRecebido is >= 0m and <= 1m, "Resposta do modelo inconsistente: escore fora de 0 a 1.");
            escore = escoreRecebido;
        }

        _resultados.Clear();
        _resultados.AddRange(novos);
        ModeloIAId = modelo.Id;
        Indicadores = indicadores;
        EscoreRisco = escore;
        Status = StatusAnalise.Concluida;
        ConcluidaEm = agora;
        MensagemErro = null;
    }

    public void Falhar(string mensagem)
    {
        Regra.Exigir(Status == StatusAnalise.Processando, "A análise não está em processamento.");
        Status = StatusAnalise.Erro;
        MensagemErro = Regra.TextoObrigatorio(mensagem, "Mensagem de erro", 500);
    }

    public void RegistrarObservacao(string? observacao)
    {
        GarantirConcluida();
        ObservacaoMedico = Regra.TextoOpcional(observacao, "Observação", 2000);
    }

    /// <summary>O médico confirma que registrou no prontuário o uso da IA como apoio (Res. CFM 2.454/2026).</summary>
    public void ConfirmarRegistroProntuario(DateTimeOffset agora)
    {
        GarantirConcluida();
        Regra.Exigir(RegistradoProntuarioEm is null, "O registro no prontuário já foi confirmado.");
        RegistradoProntuarioEm = agora;
    }

    private void GarantirConcluida()
        => Regra.Exigir(Status == StatusAnalise.Concluida, "A análise ainda não foi concluída.");

    private static void ValidarProbabilidades(ResultadoDedoRecebido recebido)
    {
        var probabilidades = recebido.Probabilidades;
        var nome = recebido.Posicao.ToString();

        Regra.Exigir(
            probabilidades is not null
            && probabilidades.Count == Enum.GetValues<ClasseImagem>().Length
            && Enum.GetValues<ClasseImagem>().All(probabilidades.ContainsKey),
            $"Resposta do modelo inconsistente: probabilidades incompletas em {nome}.");
        Regra.Exigir(probabilidades!.Values.All(p => p is >= 0m and <= 1m),
            $"Resposta do modelo inconsistente: probabilidade fora de 0 a 1 em {nome}.");
        Regra.Exigir(Math.Abs(probabilidades.Values.Sum() - 1m) <= ToleranciaSomaProbabilidades,
            $"Resposta do modelo inconsistente: probabilidades de {nome} não somam 1.");
        Regra.Exigir(recebido.Confianca is >= 0m and <= 1m,
            $"Resposta do modelo inconsistente: confiança fora de 0 a 1 em {nome}.");
    }
}
