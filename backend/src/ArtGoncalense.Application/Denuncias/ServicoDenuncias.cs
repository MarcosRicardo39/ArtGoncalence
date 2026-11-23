using ArtGoncalense.Domain;

namespace ArtGoncalense.Application.Denuncias;

public enum ResultadoEncerramento
{
    Encerrada,
    NaoEncontrada,
    JaEncerrada
}

public sealed record DenunciaRecebida(
    Guid Id, Guid IlustracaoId, string Motivo, string Descricao, string? Contato, DateTimeOffset CriadoEm);

public sealed record FilaDenuncias(IReadOnlyList<DenunciaRecebida> Itens, int Total);

public interface IRepositorioDenuncias
{
    Task<bool> RegistrarAsync(Denuncia denuncia, CancellationToken cancellationToken);
    Task<FilaDenuncias> ListarRecebidasAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<ResultadoEncerramento> EncerrarAsync(Guid id, Guid moderadorId, string justificativa,
        DateTimeOffset encerradaEm, CancellationToken cancellationToken);
}

public sealed class ServicoDenuncias(IRepositorioDenuncias repositorio, TimeProvider relogio)
{
    public Task<bool> RegistrarAsync(Guid ilustracaoId, string motivo, string descricao, string? contato,
        CancellationToken cancellationToken)
    {
        var denuncia = new Denuncia(Guid.NewGuid(), ilustracaoId, motivo, descricao.Trim(), Denuncia.Recebida,
            relogio.GetUtcNow(), string.IsNullOrWhiteSpace(contato) ? null : contato.Trim());
        return repositorio.RegistrarAsync(denuncia, cancellationToken);
    }

    public Task<FilaDenuncias> ListarRecebidasAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        repositorio.ListarRecebidasAsync(page, pageSize, cancellationToken);

    public Task<ResultadoEncerramento> EncerrarAsync(Guid id, Guid moderadorId, string justificativa,
        CancellationToken cancellationToken) =>
        repositorio.EncerrarAsync(id, moderadorId, justificativa.Trim(), relogio.GetUtcNow(), cancellationToken);
}
