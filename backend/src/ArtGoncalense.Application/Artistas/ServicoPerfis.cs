using ArtGoncalense.Domain;
using System.Text.Json.Serialization;

namespace ArtGoncalense.Application.Artistas;

public sealed record PerfilPublico(Guid Id, string NomePublico, string? Bio,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ApoioPublico? Apoio = null);

public sealed record PerfilConsultado(Guid Id, string NomePublico, string? Bio, string? UrlApoio);

public enum ResultadoApoio
{
    Atualizado,
    ArtistaIndisponivel,
    UrlInvalida
}

public interface IRepositorioPerfis
{
    Task<PerfilConsultado?> BuscarPublicoAsync(Guid id, CancellationToken cancellationToken);
    Task<Usuario?> BuscarParaEdicaoAsync(Guid id, CancellationToken cancellationToken);
    Task SalvarAsync(CancellationToken cancellationToken);
    Task<bool> DefinirApoioAsync(Guid titularId, string? url, CancellationToken cancellationToken);
}

public sealed class ServicoPerfis(IRepositorioPerfis repositorio)
{
    public async Task<PerfilPublico?> BuscarPublicoAsync(Guid id, CancellationToken cancellationToken)
    {
        var perfil = await repositorio.BuscarPublicoAsync(id, cancellationToken);
        return perfil is null
            ? null
            : new PerfilPublico(perfil.Id, perfil.NomePublico, perfil.Bio, PoliticaApoio.Validar(perfil.UrlApoio));
    }

    public async Task<ResultadoApoio> DefinirApoioAsync(Guid titularId, string? url, CancellationToken cancellationToken)
    {
        var apoio = PoliticaApoio.Validar(url);
        if (url is not null && apoio is null)
        {
            return ResultadoApoio.UrlInvalida;
        }

        return await repositorio.DefinirApoioAsync(titularId, apoio?.Url, cancellationToken)
            ? ResultadoApoio.Atualizado
            : ResultadoApoio.ArtistaIndisponivel;
    }

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
