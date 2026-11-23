namespace ArtGoncalense.Domain;

public sealed class Ilustracao
{
    public Ilustracao(Guid id, Guid usuarioId, string titulo, string descricao, CategoriaIlustracao categoria,
        string nomeArquivo, string caminhoStorage, string mimeType, long tamanhoBytes, DateTimeOffset criadoEm)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(usuarioId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        ArgumentNullException.ThrowIfNull(descricao);
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeArquivo);
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoStorage);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tamanhoBytes);
        if (!Enum.IsDefined(categoria))
        {
            throw new ArgumentOutOfRangeException(nameof(categoria));
        }

        Id = id;
        UsuarioId = usuarioId;
        Titulo = titulo;
        Descricao = descricao;
        Categoria = categoria;
        NomeArquivo = nomeArquivo;
        CaminhoStorage = caminhoStorage;
        MimeType = mimeType;
        TamanhoBytes = tamanhoBytes;
        Status = StatusIlustracao.Pendente;
        CriadoEm = criadoEm;
        AtualizadoEm = criadoEm;
    }

    public Guid Id { get; }
    public Guid UsuarioId { get; }
    public string Titulo { get; }
    public string Descricao { get; }
    public CategoriaIlustracao Categoria { get; }
    public string NomeArquivo { get; }
    public string CaminhoStorage { get; }
    public string MimeType { get; }
    public long TamanhoBytes { get; }
    public StatusIlustracao Status { get; private set; }
    public DateTimeOffset CriadoEm { get; }
    public DateTimeOffset AtualizadoEm { get; private set; }
}
