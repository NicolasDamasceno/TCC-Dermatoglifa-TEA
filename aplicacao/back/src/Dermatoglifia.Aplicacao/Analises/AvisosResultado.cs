namespace Dermatoglifia.Aplicacao.Analises;

/// <summary>
/// Textos exibidos com todo resultado (RN04; UC08). Ficam no back-end, versionados:
/// cada análise guarda a versão do aviso que recebeu, e o texto daquela versão é
/// o que volta para o front, mesmo depois que o texto atual mudar.
/// Para mudar um texto, crie uma versão nova — nunca altere uma existente.
/// </summary>
public static class AvisosResultado
{
    public const string VersaoAtual = "v1";

    private static readonly Dictionary<string, Textos> PorVersao = new(StringComparer.Ordinal)
    {
        ["v1"] = new Textos(
            Aviso: "Protótipo de pesquisa, não validado clinicamente e não aprovado pela Anvisa. " +
                   "Não constitui diagnóstico nem triagem de TEA e não substitui a avaliação clínica " +
                   "nem instrumentos validados como o M-CHAT.",
            Alertas: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CodigosAlerta.SemPontoDeCorteValidado] =
                    "Não existe ponto de corte validado para estes indicadores: nenhum estudo publicado " +
                    "relata sensibilidade, especificidade ou AUC da dermatoglifia para TEA.",
                [CodigosAlerta.EscoreNaoValidado] =
                    "O escore de risco não é exibido porque o modelo de risco (Etapa 2) ainda não foi " +
                    "validado; são apresentados apenas os indicadores.",
                [CodigosAlerta.ModeloSintetico] =
                    "Resultado de demonstração: o modelo em uso foi treinado apenas com dados sintéticos " +
                    "e não deve ser interpretado.",
                [CodigosAlerta.PoucosDadosFemininos] =
                    "Os estudos de dermatoglifia em TEA usaram quase só meninos; para pacientes do sexo " +
                    "feminino a interpretação é ainda mais limitada.",
                [CodigosAlerta.QualidadeImagem] =
                    "Uma ou mais digitais foram sinalizadas com baixa qualidade; considere refazer a coleta.",
            }),
    };

    public static string Aviso(string versao) => Obter(versao).Aviso;

    public static string Alerta(string versao, string codigo) => Obter(versao).Alertas[codigo];

    private static Textos Obter(string versao)
        => PorVersao.TryGetValue(versao, out var textos)
            ? textos
            : throw new InvalidOperationException($"Versão de aviso desconhecida: {versao}.");

    private sealed record Textos(string Aviso, IReadOnlyDictionary<string, string> Alertas);
}

public static class CodigosAlerta
{
    public const string SemPontoDeCorteValidado = "SEM_PONTO_DE_CORTE_VALIDADO";
    public const string EscoreNaoValidado = "ESCORE_NAO_VALIDADO";
    public const string ModeloSintetico = "MODELO_SINTETICO";
    public const string PoucosDadosFemininos = "POUCOS_DADOS_FEMININOS";
    public const string QualidadeImagem = "QUALIDADE_IMAGEM";
}
