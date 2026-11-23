using System.Security.Claims;
using ArtGoncalense.Application.Contas;
using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ArtGoncalense.Infrastructure.Identidade;

public sealed class PrincipalContaFactory(
    UserManager<ContaIdentity> manager,
    IOptions<IdentityOptions> options,
    ArtGoncalenseDbContext db) : UserClaimsPrincipalFactory<ContaIdentity>(manager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ContaIdentity conta)
    {
        var identity = await base.GenerateClaimsAsync(conta);
        var usuario = await db.Usuarios.FindAsync(conta.Id);
        if (usuario is null || usuario.Status == StatusUsuario.Suspenso)
        {
            return identity;
        }

        identity.AddClaim(new Claim(AcessoConta.ContaDisponivel, bool.TrueString));
        identity.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, usuario.Papel.ToString()));
        if (conta.EmailConfirmed && usuario.Status == StatusUsuario.Ativo)
        {
            identity.AddClaim(new Claim(AcessoConta.EmailConfirmado, bool.TrueString));
        }

        return identity;
    }
}
