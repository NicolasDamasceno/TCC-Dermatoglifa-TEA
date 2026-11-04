namespace Dermatoglifia.Aplicacao.Excecoes;

/// <summary>Recurso inexistente — ou que pertence a outro médico (RN05). A API responde 404.</summary>
public sealed class NaoEncontradoException(string mensagem) : Exception(mensagem);

/// <summary>Conflito com dado existente (e-mail, CRM). A API responde 409.</summary>
public sealed class ConflitoException(string mensagem) : Exception(mensagem);

/// <summary>Arquivo enviado não é de um formato aceito. A API responde 415.</summary>
public sealed class FormatoNaoSuportadoException(string mensagem) : Exception(mensagem);

/// <summary>Arquivo enviado acima do tamanho permitido. A API responde 413.</summary>
public sealed class ConteudoMuitoGrandeException(string mensagem) : Exception(mensagem);

/// <summary>
/// O Serviço de IA não respondeu de forma utilizável (indisponível, tempo esgotado,
/// erro HTTP, JSON inválido). Não é erro da requisição do médico: a análise vai
/// para ERRO com esta mensagem e pode ser repetida (UC07, 2a).
/// </summary>
public sealed class FalhaInferenciaException(string mensagem, Exception? causa = null) : Exception(mensagem, causa);
