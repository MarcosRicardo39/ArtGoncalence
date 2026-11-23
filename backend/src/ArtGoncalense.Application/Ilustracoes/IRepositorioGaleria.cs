using ArtGoncalense.Domain;

namespace ArtGoncalense.Application.Ilustracoes;

public sealed record IlustracaoPublica(
    Guid Id, string Titulo, string Descricao, CategoriaIlustracao Categoria, DateTimeOffset CriadoEm,
    Guid ArtistaId, string NomePublico);

public sealed record GaleriaPublica(IReadOnlyList<IlustracaoPublica> Itens, int Total);

public interface IRepositorioGaleria
{
    Task<GaleriaPublica> ListarAsync(int pagina, int tamanho, CancellationToken cancellationToken);
    Task<IlustracaoPublica?> BuscarAsync(Guid id, CancellationToken cancellationToken);
    Task<string?> BuscarChaveAsync(Guid id, CancellationToken cancellationToken);
}
