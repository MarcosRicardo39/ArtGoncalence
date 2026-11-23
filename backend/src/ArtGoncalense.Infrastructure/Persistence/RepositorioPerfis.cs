using ArtGoncalense.Application.Artistas;
using ArtGoncalense.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtGoncalense.Infrastructure.Persistence;

public sealed class RepositorioPerfis(ArtGoncalenseDbContext db) : IRepositorioPerfis
{
    public Task<PerfilPublico?> BuscarPublicoAsync(Guid id, CancellationToken cancellationToken) =>
        ArtistasDisponiveis().AsNoTracking()
            .Where(usuario => usuario.Id == id)
            .Select(usuario => new PerfilPublico(usuario.Id, usuario.NomePublico, usuario.Bio))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Usuario?> BuscarParaEdicaoAsync(Guid id, CancellationToken cancellationToken) =>
        ArtistasDisponiveis().SingleOrDefaultAsync(usuario => usuario.Id == id, cancellationToken);

    public Task SalvarAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private IQueryable<Usuario> ArtistasDisponiveis() =>
        db.Usuarios.Where(usuario => usuario.Papel == PapelUsuario.Artista && usuario.Status != StatusUsuario.Suspenso);
}
