using ArtGoncalense.Application.Imagens;

namespace ArtGoncalense.Infrastructure.Imagens;

public sealed class ArmazenamentoImagensLocal : IArmazenamentoImagens
{
    private const string Extensao = ".webp";
    private const string ExtensaoTemporaria = ".tmp";
    private readonly string diretorio;

    public ArmazenamentoImagensLocal(string diretorio)
    {
        if (!Path.IsPathFullyQualified(diretorio))
        {
            throw new ArgumentException("Informe um diretório absoluto e privado para as imagens.", nameof(diretorio));
        }

        this.diretorio = Path.GetFullPath(diretorio);
    }

    public async Task<string> SalvarAsync(ImagemOtimizada imagem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dados = imagem.Bytes;
        if (dados.Length is < 12 or > LimitesImagem.SaidaBytes
            || !dados.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !dados.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            throw new ArgumentException("O armazenamento aceita apenas WebP otimizado.", nameof(imagem));
        }

        Directory.CreateDirectory(diretorio);
        var chave = Guid.NewGuid().ToString("N") + Extensao;
        var destino = Caminho(chave);
        var temporario = destino + ExtensaoTemporaria;
        var temporarioCriado = false;
        try
        {
            await using (var arquivo = new FileStream(temporario, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 16 * 1024, FileOptions.Asynchronous))
            {
                temporarioCriado = true;
                await arquivo.WriteAsync(dados, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporario, destino, overwrite: false);
            temporarioCriado = false;
            return chave;
        }
        finally
        {
            if (temporarioCriado)
            {
                File.Delete(temporario);
            }
        }
    }

    public Task<Stream?> AbrirAsync(string chave, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminho = Caminho(chave);
        try
        {
            if ((File.GetAttributes(caminho) & FileAttributes.ReparsePoint) != 0)
            {
                return Task.FromResult<Stream?>(null);
            }

            return Task.FromResult<Stream?>(new FileStream(
                caminho, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.Asynchronous));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task ExcluirAsync(string chave, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminho = Caminho(chave);
        try
        {
            File.Delete(caminho);
        }
        catch (DirectoryNotFoundException)
        {
            // O objeto já não existe.
        }

        return Task.CompletedTask;
    }

    internal IEnumerable<string> ListarChavesAntigas(DateTimeOffset anteriorA)
    {
        return ArquivosAntigos(anteriorA).Select(arquivo => arquivo.Name).Where(ChaveValida);
    }

    internal int LimparTemporariosAntigos(DateTimeOffset anteriorA, CancellationToken cancellationToken)
    {
        var removidos = 0;
        foreach (var arquivo in ArquivosAntigos(anteriorA))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (arquivo.Name.EndsWith(ExtensaoTemporaria, StringComparison.Ordinal)
                && ChaveValida(arquivo.Name[..^ExtensaoTemporaria.Length]))
            {
                arquivo.Delete();
                removidos++;
            }
        }

        return removidos;
    }

    private IEnumerable<FileInfo> ArquivosAntigos(DateTimeOffset anteriorA)
    {
        var raiz = new DirectoryInfo(diretorio);
        return raiz.Exists
            ? raiz.EnumerateFiles().Where(arquivo => arquivo.LastWriteTimeUtc < anteriorA.UtcDateTime
                && (arquivo.Attributes & FileAttributes.ReparsePoint) == 0)
            : [];
    }

    private string Caminho(string chave)
    {
        if (!ChaveValida(chave))
        {
            throw new ArgumentException("Chave de imagem inválida.", nameof(chave));
        }

        return Path.Combine(diretorio, chave);
    }

    private static bool ChaveValida(string chave) =>
        chave.Length == 32 + Extensao.Length && chave.EndsWith(Extensao, StringComparison.Ordinal)
        && Guid.TryParseExact(chave.AsSpan(0, 32), "N", out var id)
        && string.Equals(chave, id.ToString("N") + Extensao, StringComparison.Ordinal);
}
