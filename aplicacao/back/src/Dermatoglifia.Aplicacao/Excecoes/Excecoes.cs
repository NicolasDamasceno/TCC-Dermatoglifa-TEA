namespace Dermatoglifia.Aplicacao.Excecoes;

/// <summary>Recurso inexistente — ou que pertence a outro médico (RN05). A API responde 404.</summary>
public sealed class NaoEncontradoException(string mensagem) : Exception(mensagem);

/// <summary>Conflito com dado existente (e-mail, CRM). A API responde 409.</summary>
public sealed class ConflitoException(string mensagem) : Exception(mensagem);
