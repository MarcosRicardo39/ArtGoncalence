namespace ArtGoncalense.Domain;

public sealed class Denuncia
{
    public const string Recebida = "RECEBIDA";
    public const string Encerrada = "ENCERRADA";

    public Denuncia(Guid id, Guid ilustracaoId, string motivo, string descricao, string status,
        DateTimeOffset criadoEm, string? contato = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(ilustracaoId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentNullException.ThrowIfNull(descricao);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Id = id;
        IlustracaoId = ilustracaoId;
        Motivo = motivo;
        Descricao = descricao;
        Status = status;
        CriadoEm = criadoEm;
        Contato = contato;
    }

    public Guid Id { get; }
    public Guid IlustracaoId { get; }
    public string? Contato { get; }
    public string Motivo { get; }
    public string Descricao { get; }
    public string Status { get; private set; }
    public DateTimeOffset CriadoEm { get; }
    public Guid? EncerradaPorId { get; private set; }
    public DateTimeOffset? EncerradaEm { get; private set; }
    public string? JustificativaEncerramento { get; private set; }
}
