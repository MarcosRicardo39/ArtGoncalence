namespace ArtGoncalense.Application.Contas;

public interface IServicoContas
{
    bool EnvioDisponivel { get; }

    Task<bool> RegistrarAsync(string email, string senha, string nomePublico, CancellationToken cancellationToken);
    Task<bool> LoginAsync(string email, string senha, CancellationToken cancellationToken);
    Task SairAsync();
    Task<bool> ConfirmarAsync(string email, string codigo, CancellationToken cancellationToken);
    Task ReenviarConfirmacaoAsync(string email, CancellationToken cancellationToken);
    Task SolicitarRecuperacaoAsync(string email, CancellationToken cancellationToken);
    Task<bool> RedefinirSenhaAsync(string email, string codigo, string senha, CancellationToken cancellationToken);
}

public static class AcessoConta
{
    public const string PapelArtista = "Artista";
    public const string ContaDisponivel = "conta_disponivel";
    public const string EmailConfirmado = "email_confirmado";
    public const string PublicarIlustracao = "PublicarIlustracao";
    public const int SenhaMinima = 15;
    public const int SenhaMaxima = 128;
}
