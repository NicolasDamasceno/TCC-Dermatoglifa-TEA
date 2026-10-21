using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Dominio.Enums;

namespace Dermatoglifia.Aplicacao.Autenticacao;

/// <summary>UC01 — Autenticar-se.</summary>
public sealed class ServicoAutenticacao(
    IContas contas,
    IEmissorToken emissorToken,
    IAuditoria auditoria,
    IContextoDados contexto)
{
    public async Task<ResultadoEntrada> EntrarAsync(string email, string senha, CancellationToken cancelamento = default)
    {
        var resultado = await contas.VerificarCredenciaisAsync(email, senha, cancelamento);

        var (acao, saida) = resultado.Situacao switch
        {
            SituacaoCredenciais.Validas => ("LOGIN_OK", ResultadoEntrada.Sucesso(resultado.Conta!, emissorToken.Emitir(resultado.Conta!))),
            SituacaoCredenciais.Bloqueada => ("LOGIN_BLOQUEADO", ResultadoEntrada.Falha(MotivoFalhaEntrada.Bloqueada)),
            SituacaoCredenciais.ContaNaoAtiva => ("LOGIN_CONTA_NAO_ATIVA", ResultadoEntrada.Falha(MotivoDaConta(resultado.Status))),
            _ => ("LOGIN_FALHA", ResultadoEntrada.Falha(MotivoFalhaEntrada.CredenciaisInvalidas)),
        };

        auditoria.Registrar(acao, "usuario", resultado.UsuarioId, usuarioId: resultado.UsuarioId);
        await contexto.SalvarAsync(cancelamento);
        return saida;
    }

    private static MotivoFalhaEntrada MotivoDaConta(StatusCadastro? status) => status switch
    {
        StatusCadastro.Pendente => MotivoFalhaEntrada.CadastroPendente,
        StatusCadastro.Recusado => MotivoFalhaEntrada.CadastroRecusado,
        _ => MotivoFalhaEntrada.CadastroInativo,
    };
}

public enum MotivoFalhaEntrada { CredenciaisInvalidas, Bloqueada, CadastroPendente, CadastroRecusado, CadastroInativo }

public sealed record ResultadoEntrada(bool Sucedeu, ContaAutenticada? Conta, TokenEmitido? Token, MotivoFalhaEntrada? Motivo)
{
    public static ResultadoEntrada Sucesso(ContaAutenticada conta, TokenEmitido token) => new(true, conta, token, null);

    public static ResultadoEntrada Falha(MotivoFalhaEntrada motivo) => new(false, null, null, motivo);
}
