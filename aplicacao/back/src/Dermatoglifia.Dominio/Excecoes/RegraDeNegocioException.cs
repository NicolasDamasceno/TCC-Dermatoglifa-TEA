namespace Dermatoglifia.Dominio.Excecoes;

/// <summary>
/// Violação de uma regra do domínio. A mensagem é escrita para ser exibida ao
/// usuário; a API a converte em resposta 422 (doc 08, seção 5).
/// </summary>
public sealed class RegraDeNegocioException(string mensagem) : Exception(mensagem);
