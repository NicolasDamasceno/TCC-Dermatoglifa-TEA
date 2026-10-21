using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Aplicacao.Excecoes;
using Dermatoglifia.Dominio.Entidades;
using Dermatoglifia.Dominio.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dermatoglifia.Aplicacao.Medicos;

/// <summary>UC02 — Solicitar cadastro de médico; UC03 — Aprovar cadastro de médico.</summary>
public sealed class ServicoMedicos(
    IContextoDados contexto,
    IContas contas,
    IAuditoria auditoria,
    IUsuarioAtual usuarioAtual,
    IRelogio relogio)
{
    public async Task<Guid> SolicitarCadastroAsync(SolicitacaoCadastro solicitacao, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);

        // Valida as regras do domínio antes de criar qualquer coisa.
        var usuarioId = Guid.CreateVersion7();
        var medico = Medico.Solicitar(usuarioId, solicitacao.Crm, solicitacao.UfCrm, solicitacao.Especialidade);

        if (await contexto.Medicos.AnyAsync(m => m.Crm == medico.Crm && m.UfCrm == medico.UfCrm, cancelamento))
        {
            throw new ConflitoException("Já existe cadastro com este CRM nesta UF.");
        }

        // A conta é gravada pelo Identity; o médico e a auditoria, em seguida —
        // tudo na mesma transação.
        await using var transacao = await contexto.IniciarTransacaoAsync(cancelamento);
        await contas.CriarMedicoPendenteAsync(usuarioId, solicitacao.Nome, solicitacao.Email, solicitacao.Senha, cancelamento);
        contexto.Medicos.Add(medico);
        auditoria.Registrar("MEDICO_SOLICITOU_CADASTRO", "medico", usuarioId, usuarioId: usuarioId);
        await contexto.SalvarAsync(cancelamento);
        await transacao.CommitAsync(cancelamento);

        return usuarioId;
    }

    public async Task<IReadOnlyList<SolicitacaoPendente>> ListarPendentesAsync(CancellationToken cancelamento = default)
    {
        var pendentes = await contexto.Medicos
            .AsNoTracking()
            .Where(m => m.AprovadoEm == null && m.MotivoRecusa == null)
            .ToListAsync(cancelamento);

        var contasPorId = await contas.ObterResumosAsync(pendentes.Select(m => m.UsuarioId), cancelamento);

        return pendentes
            .Where(m => contasPorId.ContainsKey(m.UsuarioId))
            .Select(m =>
            {
                var conta = contasPorId[m.UsuarioId];
                return new SolicitacaoPendente(m.UsuarioId, conta.Nome, conta.Email, m.Crm, m.UfCrm, m.Especialidade, conta.CriadoEm);
            })
            .OrderBy(s => s.SolicitadoEm)
            .ToList();
    }

    public async Task AprovarAsync(Guid medicoId, string vinculoProtocolo, CancellationToken cancelamento = default)
    {
        var medico = await ObterAsync(medicoId, cancelamento);
        medico.Aprovar(usuarioAtual.IdObrigatorio, vinculoProtocolo, relogio.Agora);
        await contas.AlterarStatusAsync(medicoId, StatusCadastro.Ativo, cancelamento);

        auditoria.Registrar("MEDICO_APROVADO", "medico", medicoId);
        await contexto.SalvarAsync(cancelamento);
    }

    public async Task RecusarAsync(Guid medicoId, string motivo, CancellationToken cancelamento = default)
    {
        var medico = await ObterAsync(medicoId, cancelamento);
        medico.Recusar(motivo);
        await contas.AlterarStatusAsync(medicoId, StatusCadastro.Recusado, cancelamento);

        auditoria.Registrar("MEDICO_RECUSADO", "medico", medicoId);
        await contexto.SalvarAsync(cancelamento);
    }

    private async Task<Medico> ObterAsync(Guid medicoId, CancellationToken cancelamento)
        => await contexto.Medicos.SingleOrDefaultAsync(m => m.UsuarioId == medicoId, cancelamento)
           ?? throw new NaoEncontradoException("Cadastro de médico não encontrado.");
}

public sealed record SolicitacaoCadastro(string Nome, string Email, string Senha, string Crm, string UfCrm, string Especialidade);

public sealed record SolicitacaoPendente(
    Guid Id, string Nome, string Email, string Crm, string UfCrm, string Especialidade, DateTimeOffset SolicitadoEm);
