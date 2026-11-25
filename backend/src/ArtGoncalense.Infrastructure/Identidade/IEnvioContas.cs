namespace ArtGoncalense.Infrastructure.Identidade;

public interface IEnvioContas
{
    bool Disponivel { get; }
    bool PermiteDestinatario(string email);
    Task EnviarAsync(string email, string finalidade, string codigo, CancellationToken cancellationToken);
}
