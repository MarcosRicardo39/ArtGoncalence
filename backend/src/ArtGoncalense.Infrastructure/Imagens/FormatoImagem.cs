using System.Buffers.Binary;
using ArtGoncalense.Application.Imagens;
using ImageMagick;

namespace ArtGoncalense.Infrastructure.Imagens;

internal static class FormatoImagem
{
    public static MagickFormat Validar(ReadOnlySpan<byte> bytes, string nomeArquivo, string tipoDeclarado)
    {
        var (formato, tipo) = Path.GetExtension(nomeArquivo).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => (MagickFormat.Jpeg, "image/jpeg"),
            ".png" => (MagickFormat.Png, "image/png"),
            ".webp" => (MagickFormat.WebP, ImagemOtimizada.Tipo),
            _ => throw new ImagemInvalidaException(MotivoImagemInvalida.Formato, "Use JPEG, PNG ou WebP estático.")
        };

        var assinaturaValida = formato switch
        {
            MagickFormat.Jpeg => bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
            MagickFormat.Png => bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            MagickFormat.WebP => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8)
                && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

        if (!string.Equals(tipo, tipoDeclarado, StringComparison.OrdinalIgnoreCase) || !assinaturaValida)
        {
            throw new ImagemInvalidaException(MotivoImagemInvalida.Formato,
                "A extensão, o tipo e o conteúdo da imagem precisam corresponder.");
        }

        if (formato == MagickFormat.Png)
        {
            ValidarPng(bytes[8..]);
        }
        else if (formato == MagickFormat.WebP)
        {
            ValidarWebP(bytes);
        }

        return formato;
    }

    private static void ValidarPng(ReadOnlySpan<byte> blocos)
    {
        if (blocos.Length < 25 || BinaryPrimitives.ReadUInt32BigEndian(blocos) != 13
            || !blocos.Slice(4, 4).SequenceEqual("IHDR"u8))
        {
            throw ConteudoInvalido();
        }

        var largura = BinaryPrimitives.ReadUInt32BigEndian(blocos.Slice(8, 4));
        var altura = BinaryPrimitives.ReadUInt32BigEndian(blocos.Slice(12, 4));
        if (largura is 0 or > LimitesImagem.EntradaLado || altura is 0 or > LimitesImagem.EntradaLado)
        {
            throw DimensoesInvalidas();
        }

        while (blocos.Length >= 12)
        {
            var tamanho = BinaryPrimitives.ReadUInt32BigEndian(blocos);
            if (tamanho > blocos.Length - 12)
            {
                break;
            }

            var tipo = blocos.Slice(4, 4);
            if (tipo.SequenceEqual("acTL"u8) || tipo.SequenceEqual("fcTL"u8) || tipo.SequenceEqual("fdAT"u8))
            {
                throw AnimacaoInvalida();
            }

            if (tipo.SequenceEqual("IEND"u8) && tamanho == 0 && blocos.Length == 12)
            {
                return;
            }

            blocos = blocos[((int)tamanho + 12)..];
        }

        throw ConteudoInvalido();
    }

    private static void ValidarWebP(ReadOnlySpan<byte> bytes)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4)) != bytes.Length - 8)
        {
            throw ConteudoInvalido();
        }

        var blocos = bytes[12..];
        while (blocos.Length >= 8)
        {
            var tipo = blocos[..4];
            var tamanho = BinaryPrimitives.ReadUInt32LittleEndian(blocos.Slice(4, 4));
            var tamanhoComPadding = (ulong)tamanho + (tamanho & 1u);
            if (tamanhoComPadding > (ulong)(blocos.Length - 8))
            {
                throw ConteudoInvalido();
            }

            if (tipo.SequenceEqual("ANIM"u8) || tipo.SequenceEqual("ANMF"u8)
                || (tipo.SequenceEqual("VP8X"u8) && tamanho > 0 && (blocos[8] & 0x02) != 0))
            {
                throw AnimacaoInvalida();
            }

            blocos = blocos[((int)tamanhoComPadding + 8)..];
        }

        if (!blocos.IsEmpty)
        {
            throw ConteudoInvalido();
        }
    }

    private static ImagemInvalidaException AnimacaoInvalida() =>
        new(MotivoImagemInvalida.Formato, "Envie uma imagem estática.");

    internal static ImagemInvalidaException ConteudoInvalido() =>
        new(MotivoImagemInvalida.Conteudo, "O arquivo não pôde ser decodificado como imagem.");

    internal static ImagemInvalidaException DimensoesInvalidas() =>
        new(MotivoImagemInvalida.Dimensoes, "A imagem deve ter no máximo 4.096 pixels por lado.");
}
