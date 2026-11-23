namespace ArtGoncalense.Domain;

public sealed class Usuario
{
    public Usuario(Guid id, string email, string nomePublico, PapelUsuario papel, StatusUsuario status,
        DateTimeOffset criadoEm, string? bio = null, string? avatarPath = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(nomePublico);
        if (!Enum.IsDefined(papel))
        {
            throw new ArgumentOutOfRangeException(nameof(papel));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Id = id;
        Email = email;
        NomePublico = nomePublico;
        Papel = papel;
        Status = status;
        CriadoEm = criadoEm;
        Bio = bio;
        AvatarPath = avatarPath;
    }

    public Guid Id { get; }
    public string Email { get; }
    public string NomePublico { get; }
    public string? Bio { get; }
    public string? AvatarPath { get; }
    public PapelUsuario Papel { get; }
    public StatusUsuario Status { get; private set; }
    public DateTimeOffset CriadoEm { get; }

    public void ConfirmarEmail()
    {
        if (Status == StatusUsuario.PendenteConfirmacao)
        {
            Status = StatusUsuario.Ativo;
        }
    }
}
