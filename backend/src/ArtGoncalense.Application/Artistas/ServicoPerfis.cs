using ArtGoncalense.Domain;

namespace ArtGoncalense.Application.Artistas;

public sealed record PerfilPublico(Guid Id, string NomePublico, string? Bio);

public interface IRepositorioPerfis
{
    Task<PerfilPublico?> BuscarPublicoAsync(Guid id, CancellationToken cancellationToken);
    Task<Usuario?> BuscarParaEdicaoAsync(Guid id, CancellationToken cancellationToken);
    Task SalvarAsync(CancellationToken cancellationToken);
}

public sealed class ServicoPerfis(IRepositorioPerfis repositorio)
{
    public Task<PerfilPublico?> BuscarPublicoAsync(Guid id, CancellationToken cancellationToken) =>
        repositorio.BuscarPublicoAsync(id, cancellationToken);

    public async Task<bool> EditarAsync(Guid titularId, string nomePublico, string? bio, CancellationToken cancellationToken)
    {
        var artista = await repositorio.BuscarParaEdicaoAsync(titularId, cancellationToken);
        if (artista is null)
        {
            return false;
        }

        artista.EditarPerfil(nomePublico, bio);
        await repositorio.SalvarAsync(cancellationToken);
        return true;
    }
}
