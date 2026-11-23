namespace ArtGoncalense.Application.Imagens;

public interface IArmazenamentoImagens
{
    Task<string> SalvarAsync(ImagemOtimizada imagem, CancellationToken cancellationToken);
    Task<Stream?> AbrirAsync(string chave, CancellationToken cancellationToken);
    Task ExcluirAsync(string chave, CancellationToken cancellationToken);
}
