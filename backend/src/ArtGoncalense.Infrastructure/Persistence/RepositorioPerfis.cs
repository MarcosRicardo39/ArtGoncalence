using System.Data;
using ArtGoncalense.Application.Artistas;
using ArtGoncalense.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtGoncalense.Infrastructure.Persistence;

public sealed class RepositorioPerfis(ArtGoncalenseDbContext db) : IRepositorioPerfis
{
    public Task<PerfilConsultado?> BuscarPublicoAsync(Guid id, CancellationToken cancellationToken) =>
        ArtistasDisponiveis().AsNoTracking()
            .Where(usuario => usuario.Id == id)
            .Select(usuario => new PerfilConsultado(usuario.Id, usuario.NomePublico, usuario.Bio,
                db.LinksApoio.Where(link => link.UsuarioId == usuario.Id && link.Ativo)
                    .OrderBy(link => link.Id).Select(link => link.Url).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Usuario?> BuscarParaEdicaoAsync(Guid id, CancellationToken cancellationToken) =>
        ArtistasDisponiveis().SingleOrDefaultAsync(usuario => usuario.Id == id, cancellationToken);

    public Task SalvarAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<bool> DefinirApoioAsync(Guid titularId, string? url, CancellationToken cancellationToken)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM "Usuarios" WHERE "Id" = {titularId} FOR NO KEY UPDATE
            """, cancellationToken);

        if (!await ArtistasDisponiveis().AnyAsync(x => x.Id == titularId, cancellationToken))
        {
            return false;
        }

        await db.LinksApoio.Where(x => x.UsuarioId == titularId).ExecuteDeleteAsync(cancellationToken);
        if (url is not null)
        {
            db.LinksApoio.Add(new LinkApoio(Guid.NewGuid(), titularId, url, ativo: true));
            await db.SaveChangesAsync(cancellationToken);
        }

        await transacao.CommitAsync(cancellationToken);
        return true;
    }

    private IQueryable<Usuario> ArtistasDisponiveis() =>
        db.Usuarios.Where(usuario => usuario.Papel == PapelUsuario.Artista && usuario.Status != StatusUsuario.Suspenso);
}
