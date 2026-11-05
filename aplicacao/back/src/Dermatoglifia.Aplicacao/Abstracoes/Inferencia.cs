using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.ObjetosDeValor;

namespace Dermatoglifia.Aplicacao.Abstracoes;

/// <summary>
/// Chamada ao Serviço de IA (ADR-0001). Só trafegam as 10 imagens, o sexo e a
/// idade em meses — nenhum nome ou identificador do paciente (UC07, passo 2).
/// </summary>
public interface IClienteInferencia
{
    /// <exception cref="Excecoes.FalhaInferenciaException">
    /// Serviço indisponível, tempo esgotado, resposta de erro ou em formato inválido.
    /// </exception>
    Task<RespostaInferencia> AnalisarAsync(RequisicaoInferencia requisicao, CancellationToken cancelamento = default);

    /// <summary>
    /// Qual modelo o serviço carregou (<c>GET /saude</c>). Usado na promoção de versão
    /// (UC10) para conferir versão e hash com o que de fato responde às análises.
    /// </summary>
    /// <exception cref="Excecoes.FalhaInferenciaException">Serviço fora do ar ou resposta inválida.</exception>
    Task<SaudeInferencia> ConsultarSaudeAsync(CancellationToken cancelamento = default);
}

/// <summary>
/// Estado do Serviço de IA. Sem modelo válido, <see cref="Disponivel"/> é falso e
/// <see cref="Motivo"/> explica por quê.
/// </summary>
public sealed record SaudeInferencia(
    bool Disponivel,
    string? VersaoModelo,
    string? Sha256,
    OrigemDados? OrigemDados,
    string? PastaOrigem,
    string? Motivo);

/// <param name="Imagens">PNG normalizado de cada um dos 10 dedos.</param>
public sealed record RequisicaoInferencia(
    IReadOnlyDictionary<PosicaoDedo, byte[]> Imagens, SexoBiologico Sexo, int IdadeMeses);

/// <summary>Resposta já lida do JSON; a consistência é conferida pelo domínio (<see cref="Analise.Concluir"/>).</summary>
public sealed record RespostaInferencia(
    string VersaoModelo,
    IReadOnlyList<ResultadoDedoRecebido> Digitais,
    IndicadoresDermatoglificos Indicadores,
    decimal? EscoreRisco);
