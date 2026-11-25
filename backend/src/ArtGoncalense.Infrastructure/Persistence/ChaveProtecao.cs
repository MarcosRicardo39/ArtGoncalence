namespace ArtGoncalense.Infrastructure.Persistence;

public sealed class ChaveProtecao
{
    public int Id { get; private set; }
    public required string Nome { get; init; }
    public required string Xml { get; init; }
}
