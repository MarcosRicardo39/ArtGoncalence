using System.Security.Cryptography;
using ArtGoncalense.Application.Contas;
using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArtGoncalense.Infrastructure.Identidade;

public sealed class ServicoContas(
    ArtGoncalenseDbContext db,
    UserManager<ContaIdentity> contas,
    SignInManager<ContaIdentity> sessoes,
    LimiteEnvioConta limiteEnvio,
    EnvioLocalContas envio,
    TimeProvider relogio) : IServicoContas
{
    private static readonly ContaIdentity ContaSimulada = new();
    private static readonly PasswordHasher<ContaIdentity> HasherSimulado = new();
    private static readonly string HashSimulado = HasherSimulado.HashPassword(
        ContaSimulada, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    public bool EnvioDisponivel => envio.Disponivel;

    public async Task<bool> RegistrarAsync(
        string email, string senha, string nomePublico, CancellationToken cancellationToken)
    {
        if (await contas.FindByEmailAsync(email) is not null)
        {
            HasherSimulado.VerifyHashedPassword(ContaSimulada, HashSimulado, senha);
            return true;
        }

        var id = Guid.NewGuid();
        var conta = new ContaIdentity { Id = id, Email = email, UserName = id.ToString("N") };
        db.Usuarios.Add(new Usuario(id, email, nomePublico.Trim(), PapelUsuario.Artista,
            StatusUsuario.PendenteConfirmacao, relogio.GetUtcNow()));

        try
        {
            var resultado = await contas.CreateAsync(conta, senha);
            if (!resultado.Succeeded)
            {
                return resultado.Errors.All(erro => erro.Code == nameof(IdentityErrorDescriber.DuplicateEmail));
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "EmailIndex" or "IX_Usuarios_Email"
            })
        {
            return true;
        }

        await EnviarConfirmacaoAsync(conta, cancellationToken);
        return true;
    }

    public async Task<bool> LoginAsync(string email, string senha, CancellationToken cancellationToken)
    {
        var conta = await contas.FindByEmailAsync(email);
        if (conta is null || await contas.IsLockedOutAsync(conta))
        {
            HasherSimulado.VerifyHashedPassword(ContaSimulada, HashSimulado, senha);
            return false;
        }

        var resultado = await sessoes.CheckPasswordSignInAsync(conta, senha, lockoutOnFailure: true);
        if (!resultado.Succeeded || !await db.Usuarios.AnyAsync(
            usuario => usuario.Id == conta.Id && usuario.Status != StatusUsuario.Suspenso, cancellationToken))
        {
            return false;
        }

        await sessoes.SignInAsync(conta, isPersistent: false);
        return true;
    }

    public Task SairAsync() => sessoes.SignOutAsync();

    public async Task<bool> ConfirmarAsync(string email, string codigo, CancellationToken cancellationToken)
    {
        var conta = await contas.FindByEmailAsync(email);
        if (conta is null)
        {
            return false;
        }

        var usuario = await db.Usuarios.FindAsync([conta.Id], cancellationToken);
        if (usuario is null || usuario.Status == StatusUsuario.Suspenso)
        {
            return false;
        }

        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!(await contas.ConfirmEmailAsync(conta, codigo)).Succeeded)
        {
            return false;
        }

        usuario.ConfirmarEmail();
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return true;
    }

    public async Task ReenviarConfirmacaoAsync(string email, CancellationToken cancellationToken)
    {
        var conta = await contas.FindByEmailAsync(email);
        if (conta is { EmailConfirmed: false })
        {
            await EnviarConfirmacaoAsync(conta, cancellationToken);
        }
    }

    public async Task SolicitarRecuperacaoAsync(string email, CancellationToken cancellationToken)
    {
        var conta = await contas.FindByEmailAsync(email);
        if (conta is { EmailConfirmed: true } && limiteEnvio.TentarConsumir(conta.NormalizedEmail!))
        {
            var codigo = await contas.GeneratePasswordResetTokenAsync(conta);
            await envio.EnviarAsync(conta.Email!, "recuperacao", codigo, cancellationToken);
        }
    }

    public async Task<bool> RedefinirSenhaAsync(
        string email, string codigo, string senha, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var conta = await contas.FindByEmailAsync(email);
        return conta is { EmailConfirmed: true } && (await contas.ResetPasswordAsync(conta, codigo, senha)).Succeeded;
    }

    private async Task EnviarConfirmacaoAsync(ContaIdentity conta, CancellationToken cancellationToken)
    {
        if (limiteEnvio.TentarConsumir(conta.NormalizedEmail!))
        {
            var codigo = await contas.GenerateEmailConfirmationTokenAsync(conta);
            await envio.EnviarAsync(conta.Email!, "confirmacao", codigo, cancellationToken);
        }
    }
}
