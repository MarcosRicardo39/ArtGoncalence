using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ArtGoncalense.Infrastructure.Identidade;

public sealed class EnvioLocalContas(string? diretorio, ILogger<EnvioLocalContas> logger)
{
    public bool Disponivel => !string.IsNullOrWhiteSpace(diretorio);

    internal async Task EnviarAsync(string email, string finalidade, string codigo, CancellationToken cancellationToken)
    {
        if (!Disponivel)
        {
            return;
        }

        var nome = Guid.NewGuid().ToString("N");
        var temporario = Path.Combine(diretorio!, nome + ".tmp");
        try
        {
            Directory.CreateDirectory(diretorio!);
            using (var arquivo = new FileStream(temporario, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(arquivo, new { email, finalidade, codigo }, cancellationToken: cancellationToken);
            }

            File.Move(temporario, Path.Combine(diretorio!, nome + ".json"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Falha no transporte local de e-mail da conta.");
        }
        finally
        {
            try
            {
                File.Delete(temporario);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Falha ao limpar arquivo temporário do transporte local de e-mail.");
            }
        }
    }
}
