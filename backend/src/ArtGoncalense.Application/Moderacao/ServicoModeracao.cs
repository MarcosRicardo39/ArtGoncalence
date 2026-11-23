using ArtGoncalense.Domain;

namespace ArtGoncalense.Application.Moderacao;

public enum ResultadoModeracao
{
    Aplicada,
    NaoEncontrada,
    EstadoIncompativel
}

public sealed record PendenciaModeracao(
    Guid Id, Guid ArtistaId, string Titulo, string Descricao, CategoriaIlustracao Categoria, DateTimeOffset CriadoEm);

public sealed record FilaModeracao(IReadOnlyList<PendenciaModeracao> Itens, int Total);

public interface IRepositorioModeracao
{
    Task<FilaModeracao> ListarPendentesAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<string?> BuscarChaveImagemAsync(Guid ilustracaoId, CancellationToken cancellationToken);
    Task<ResultadoModeracao> AplicarAsync(AcaoModeracao acao, StatusIlustracao estadoAnterior,
        StatusIlustracao novoEstado, CancellationToken cancellationToken);
}

public sealed class ServicoModeracao(IRepositorioModeracao repositorio, TimeProvider relogio)
{
    public Task<FilaModeracao> ListarPendentesAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        repositorio.ListarPendentesAsync(page, pageSize, cancellationToken);

    public Task<string?> BuscarChaveImagemAsync(Guid ilustracaoId, CancellationToken cancellationToken) =>
        repositorio.BuscarChaveImagemAsync(ilustracaoId, cancellationToken);

    public Task<ResultadoModeracao> DecidirAsync(Guid ilustracaoId, Guid moderadorId, TipoAcaoModeracao acao,
        string justificativa, CancellationToken cancellationToken)
    {
        var (anterior, novo) = Ilustracao.TransicaoDeModeracao(acao);
        var registro = new AcaoModeracao(Guid.NewGuid(), ilustracaoId, moderadorId, acao,
            justificativa.Trim(), relogio.GetUtcNow());

        return repositorio.AplicarAsync(registro, anterior, novo, cancellationToken);
    }
}
