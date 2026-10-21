using Dermatoglifia.Aplicacao.Abstracoes;
using Dermatoglifia.Aplicacao.Autenticacao;
using Dermatoglifia.Aplicacao.Excecoes;
using Dermatoglifia.Dominio.Enums;
using Dermatoglifia.Dominio.Excecoes;
using Dermatoglifia.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dermatoglifia.Infraestrutura.Identidade;

/// <summary>Implementação de <see cref="IContas"/> sobre o ASP.NET Core Identity.</summary>
internal sealed class Contas(UserManager<Usuario> userManager, AppDbContext contexto, IRelogio relogio) : IContas
{
    // Usada para gastar o mesmo tempo de um hash real quando o e-mail não existe,
    // dificultando descobrir quais e-mails estão cadastrados pelo tempo de resposta.
    private static readonly Usuario ContaFicticia =
        Usuario.Criar("ficticia", "ficticia@invalido", PapelUsuario.Medico, DateTimeOffset.UnixEpoch);

    public async Task CriarMedicoPendenteAsync(Guid id, string nome, string email, string senha, CancellationToken cancelamento = default)
    {
        var usuario = Usuario.Criar(nome, email, PapelUsuario.Medico, relogio.Agora, id);
        var resultado = await userManager.CreateAsync(usuario, senha);
        if (resultado.Succeeded)
        {
            return;
        }

        if (resultado.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            throw new ConflitoException("Já existe cadastro com este e-mail.");
        }

        throw new RegraDeNegocioException(string.Join(" ", resultado.Errors.Select(e => e.Description).Distinct()));
    }

    public async Task<ResultadoCredenciais> VerificarCredenciaisAsync(string email, string senha, CancellationToken cancelamento = default)
    {
        var usuario = string.IsNullOrWhiteSpace(email) ? null : await userManager.FindByEmailAsync(email.Trim());
        if (usuario is null)
        {
            userManager.PasswordHasher.HashPassword(ContaFicticia, senha ?? string.Empty);
            return ResultadoCredenciais.Invalidas(null);
        }

        if (await userManager.IsLockedOutAsync(usuario))
        {
            return ResultadoCredenciais.Bloqueada(usuario.Id);
        }

        if (!await userManager.CheckPasswordAsync(usuario, senha ?? string.Empty))
        {
            await userManager.AccessFailedAsync(usuario);
            return await userManager.IsLockedOutAsync(usuario)
                ? ResultadoCredenciais.Bloqueada(usuario.Id)
                : ResultadoCredenciais.Invalidas(usuario.Id);
        }

        await userManager.ResetAccessFailedCountAsync(usuario);

        // O status só é revelado a quem acertou a senha.
        if (!usuario.PodeAutenticar)
        {
            return ResultadoCredenciais.NaoAtiva(usuario.Id, usuario.Status);
        }

        usuario.RegistrarAcesso(relogio.Agora);
        return ResultadoCredenciais.Validas(new ContaAutenticada(
            usuario.Id, usuario.Nome, usuario.Email!, usuario.Papel, usuario.SecurityStamp!));
    }

    public async Task AlterarStatusAsync(Guid id, StatusCadastro status, CancellationToken cancelamento = default)
    {
        var usuario = await contexto.Users.SingleOrDefaultAsync(u => u.Id == id, cancelamento)
                      ?? throw new NaoEncontradoException("Conta não encontrada.");
        usuario.AlterarStatus(status);
    }

    public async Task<IReadOnlyDictionary<Guid, ResumoConta>> ObterResumosAsync(IEnumerable<Guid> ids, CancellationToken cancelamento = default)
    {
        var lista = ids.Distinct().ToList();
        return await contexto.Users
            .AsNoTracking()
            .Where(u => lista.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => new ResumoConta(u.Id, u.Nome, u.Email!, u.Papel, u.Status, u.CriadoEm),
                cancelamento);
    }

    public async Task<bool> SessaoValidaAsync(Guid id, string resumoCarimbo, CancellationToken cancelamento = default)
    {
        var conta = await contexto.Users.AsNoTracking()
            .Where(u => u.Id == id && u.Status == StatusCadastro.Ativo)
            .Select(u => new { u.SecurityStamp })
            .SingleOrDefaultAsync(cancelamento);

        return conta?.SecurityStamp is not null && CarimboSeguranca.Confere(conta.SecurityStamp, resumoCarimbo);
    }
}
