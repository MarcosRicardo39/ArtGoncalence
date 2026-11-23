namespace ArtGoncalense.Application.Imagens;

public static class LimitesImagem
{
    public const int EntradaBytes = 10_000_000;
    public const int EntradaLado = 4096;
    public const int SaidaBytes = 300_000;
    public const uint SaidaLadoMaximo = 1600;
    public const uint SaidaLadoMinimo = 1024;
}

public enum MotivoImagemInvalida
{
    Formato,
    TamanhoEntrada,
    Dimensoes,
    Conteudo,
    TamanhoSaida
}

public sealed class ImagemInvalidaException(MotivoImagemInvalida motivo, string mensagem) : Exception(mensagem)
{
    public MotivoImagemInvalida Motivo { get; } = motivo;
}

public sealed record ImagemOtimizada(byte[] Bytes, uint Largura, uint Altura)
{
    public const string Tipo = "image/webp";
}

public interface IProcessadorImagens
{
    Task<ImagemOtimizada> OtimizarAsync(
        Stream origem, string nomeArquivo, string tipoDeclarado, CancellationToken cancellationToken);
}
