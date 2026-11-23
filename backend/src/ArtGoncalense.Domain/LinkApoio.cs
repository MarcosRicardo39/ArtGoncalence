namespace ArtGoncalense.Domain;

public sealed class LinkApoio
{
    public LinkApoio(Guid id, Guid usuarioId, string url, bool ativo)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(usuarioId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        Id = id;
        UsuarioId = usuarioId;
        Url = url;
        Ativo = ativo;
    }

    public Guid Id { get; }
    public Guid UsuarioId { get; }
    public string Url { get; }
    public bool Ativo { get; }
}
