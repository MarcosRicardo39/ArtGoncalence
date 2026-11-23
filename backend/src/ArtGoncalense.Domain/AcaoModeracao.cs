namespace ArtGoncalense.Domain;

public sealed class AcaoModeracao
{
    public AcaoModeracao(Guid id, Guid ilustracaoId, Guid moderadorId, TipoAcaoModeracao acao,
        string justificativa, DateTimeOffset criadoEm)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(ilustracaoId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(moderadorId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(justificativa);
        if (!Enum.IsDefined(acao))
        {
            throw new ArgumentOutOfRangeException(nameof(acao));
        }

        Id = id;
        IlustracaoId = ilustracaoId;
        ModeradorId = moderadorId;
        Acao = acao;
        Justificativa = justificativa;
        CriadoEm = criadoEm;
    }

    public Guid Id { get; }
    public Guid IlustracaoId { get; }
    public Guid ModeradorId { get; }
    public TipoAcaoModeracao Acao { get; }
    public string Justificativa { get; }
    public DateTimeOffset CriadoEm { get; }
}
