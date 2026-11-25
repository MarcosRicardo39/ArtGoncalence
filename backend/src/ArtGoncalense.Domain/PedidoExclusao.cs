namespace ArtGoncalense.Domain;

public sealed class PedidoExclusao
{
    public PedidoExclusao(Guid id, Guid titularId, Guid? ilustracaoId, DateTimeOffset solicitadoEm)
    {
        Id = id;
        TitularId = titularId;
        IlustracaoId = ilustracaoId;
        SolicitadoEm = solicitadoEm;
    }

    public Guid Id { get; private set; }
    public Guid TitularId { get; private set; }
    public Guid? IlustracaoId { get; private set; }
    public DateTimeOffset SolicitadoEm { get; private set; }
    public DateTimeOffset? ConcluidoEm { get; private set; }

    public void Concluir(DateTimeOffset data)
    {
        TitularId = Guid.Empty;
        IlustracaoId = null;
        ConcluidoEm = data;
    }
}
