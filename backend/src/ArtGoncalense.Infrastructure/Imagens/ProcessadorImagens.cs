using System.Buffers;
using ArtGoncalense.Application.Imagens;
using ImageMagick;
using ImageMagick.Configuration;
using ImageMagick.Formats;

namespace ArtGoncalense.Infrastructure.Imagens;

public sealed class ProcessadorImagens : IProcessadorImagens
{
    private static readonly SemaphoreSlim Processamento = new(1);
    private static readonly uint[] Lados = [1440, 1280, 1152, LimitesImagem.SaidaLadoMinimo];
    private static readonly uint[] Qualidades = [85, 80, 75];

    static ProcessadorImagens()
    {
        var configuracao = ConfigurationFiles.Default;
        configuracao.Policy.Data = """
            <policymap>
              <policy domain="delegate" rights="none" pattern="*" />
              <policy domain="filter" rights="none" pattern="*" />
              <policy domain="resource" name="map" value="0" />
              <policy domain="path" rights="none" pattern="@*" />
              <policy domain="coder" rights="none" pattern="*" />
              <policy domain="coder" rights="read" pattern="{JPEG,PNG}" />
              <policy domain="coder" rights="read|write" pattern="WEBP" />
            </policymap>
            """;
        MagickNET.Initialize(configuracao);
        ResourceLimits.Width = LimitesImagem.EntradaLado;
        ResourceLimits.Height = LimitesImagem.EntradaLado;
        ResourceLimits.Area = (ulong)LimitesImagem.EntradaLado * LimitesImagem.EntradaLado;
        ResourceLimits.Memory = 192 * 1024 * 1024;
        ResourceLimits.Disk = 0;
        // O cache nativo exige comprimento estritamente menor que o limite.
        ResourceLimits.ListLength = 2;
        ResourceLimits.Thread = 1;
    }

    public async Task<ImagemOtimizada> OtimizarAsync(
        Stream origem, string nomeArquivo, string tipoDeclarado, CancellationToken cancellationToken)
    {
        await Processamento.WaitAsync(cancellationToken);
        try
        {
            using var dados = await LerEntradaAsync(origem, cancellationToken);
            var formato = FormatoImagem.Validar(dados.GetBuffer().AsSpan(0, (int)dados.Length), nomeArquivo, tipoDeclarado);
            return DecodificarEOtimizar(dados, formato, cancellationToken);
        }
        finally
        {
            Processamento.Release();
        }
    }

    private static async Task<MemoryStream> LerEntradaAsync(Stream origem, CancellationToken cancellationToken)
    {
        var dados = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            int lidos;
            while ((lidos = await origem.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
            {
                if (dados.Length + lidos > LimitesImagem.EntradaBytes)
                {
                    throw new ImagemInvalidaException(MotivoImagemInvalida.TamanhoEntrada,
                        "A imagem enviada deve ter no máximo 10 MB.");
                }

                dados.Write(buffer, 0, lidos);
            }

            dados.Position = 0;
            return dados;
        }
        catch
        {
            dados.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static ImagemOtimizada DecodificarEOtimizar(
        Stream dados, MagickFormat formato, CancellationToken cancellationToken)
    {
        try
        {
            using var imagem = new MagickImage();
            imagem.Warning += (_, _) => throw FormatoImagem.ConteudoInvalido();
            var configuracao = new MagickReadSettings { Format = formato };
            imagem.Ping(dados, configuracao);
            if (imagem.Width == 0 || imagem.Height == 0
                || imagem.Width > LimitesImagem.EntradaLado || imagem.Height > LimitesImagem.EntradaLado)
            {
                throw FormatoImagem.DimensoesInvalidas();
            }

            dados.Position = 0;
            imagem.Read(dados, configuracao);
            imagem.AutoOrient();
            imagem.ColorSpace = ColorSpace.sRGB;
            imagem.Strip();
            return Codificar(imagem, cancellationToken);
        }
        catch (MagickResourceLimitErrorException)
        {
            throw new ImagemInvalidaException(MotivoImagemInvalida.Dimensoes,
                "A imagem excedeu os limites de dimensões ou de recursos de processamento.");
        }
        catch (MagickException)
        {
            throw FormatoImagem.ConteudoInvalido();
        }
    }

    private static ImagemOtimizada Codificar(MagickImage original, CancellationToken cancellationToken)
    {
        var ladoInicial = Math.Min(Math.Max(original.Width, original.Height), LimitesImagem.SaidaLadoMaximo);
        var configuracao = new WebPWriteDefines { AlphaQuality = 100, Method = 6 };
        foreach (var lado in Lados.Where(lado => lado < ladoInicial).Prepend(ladoInicial))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var imagem = original.Clone();
            if (Math.Max(imagem.Width, imagem.Height) > lado)
            {
                imagem.Resize(lado, lado);
            }

            foreach (var qualidade in Qualidades)
            {
                cancellationToken.ThrowIfCancellationRequested();
                imagem.Quality = qualidade;
                var bytes = imagem.ToByteArray(configuracao);
                if (bytes.Length <= LimitesImagem.SaidaBytes)
                {
                    return new ImagemOtimizada(bytes, imagem.Width, imagem.Height);
                }
            }
        }

        throw new ImagemInvalidaException(MotivoImagemInvalida.TamanhoSaida,
            "A imagem não coube em 300 KB. Envie uma versão menor.");
    }

}
