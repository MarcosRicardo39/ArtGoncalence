using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Imagens;
using ArtGoncalense.Infrastructure.Persistence;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace ArtGoncalense.Tests;

[Collection("PostgreSQL local")]
public sealed class ImagensTests(ITestOutputHelper log)
{
    private const string PngTransparente = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAGHRFWHRDb21tZW50AHByaXZhdGUtbWV0YWRhdGFvg3uxAAAAEElEQVR4AQEFAPr/AP8AAAAEAQEApEStggAAAABJRU5ErkJggg==";
    private const string PngAnimado = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAGHRFWHRDb21tZW50AHByaXZhdGUtbWV0YWRhdGFvg3uxAAAACGFjVEwAAAACAAAAAPONk3AAAAAQSURBVHgBAQUA+v8A/wAAAAQBAQCkRK2CAAAAAElFTkSuQmCC";
    private const string PngLargo = "iVBORw0KGgoAAAANSUhEUgAAEAEAAAABCAYAAACx4wBCAAAAGHRFWHRDb21tZW50AHByaXZhdGUtbWV0YWRhdGFvg3uxAAAAEElEQVR4AQEFAPr/AP8AAAAEAQEApEStggAAAABJRU5ErkJggg==";

    [Theory]
    [InlineData("ilustracao-sao-goncalo.png", "image/png")]
    [InlineData("ilustracao-hq.png", "image/png")]
    [InlineData("ilustracao-anime.png", "image/png")]
    [InlineData("jpeg-minimo.jpg", "image/jpeg")]
    public async Task OtimizaAmostrasParaWebP(string arquivo, string tipo)
    {
        await using var origem = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", arquivo));

        var resultado = await new ProcessadorImagens().OtimizarAsync(origem, arquivo, tipo, CancellationToken.None);

        Assert.InRange(resultado.Bytes.Length, 12, LimitesImagem.SaidaBytes);
        Assert.InRange(Math.Max(resultado.Largura, resultado.Altura), 1u, LimitesImagem.SaidaLadoMaximo);
        using var webp = new MagickImage(resultado.Bytes);
        Assert.Equal(MagickFormat.WebP, webp.Format);
        Assert.Equal(resultado.Largura, webp.Width);
        Assert.Equal(resultado.Altura, webp.Height);
        Assert.Empty(webp.ProfileNames);
        log.WriteLine($"{arquivo}: {resultado.Bytes.Length} bytes, {resultado.Largura} × {resultado.Altura}.");
    }

    [Fact]
    public async Task PreservaTransparenciaRemoveMetadadosEAceitaWebP()
    {
        var processador = new ProcessadorImagens();
        using var origem = new MemoryStream(Convert.FromBase64String(PngTransparente));
        var resultado = await processador.OtimizarAsync(origem, "transparente.png", "image/png", CancellationToken.None);
        using var imagem = new MagickImage(resultado.Bytes);
        Assert.True(imagem.HasAlpha);
        Assert.Empty(imagem.ProfileNames);
        Assert.DoesNotContain("private-metadata", System.Text.Encoding.ASCII.GetString(resultado.Bytes));

        using var webp = new MemoryStream(resultado.Bytes);
        var reprocessado = await processador.OtimizarAsync(webp, "transparente.webp", "image/webp", CancellationToken.None);
        Assert.Equal(1u, reprocessado.Largura);
        Assert.Equal(1u, reprocessado.Altura);
    }

    [Theory]
    [InlineData("falso.png", "image/jpeg")]
    [InlineData("falso.jpg", "image/jpeg")]
    [InlineData("falso.svg", "image/svg+xml")]
    public async Task RejeitaDivergenciasDeFormato(string nome, string tipo)
    {
        using var origem = new MemoryStream(Convert.FromBase64String(PngTransparente));
        var erro = await Assert.ThrowsAsync<ImagemInvalidaException>(() =>
            new ProcessadorImagens().OtimizarAsync(origem, nome, tipo, CancellationToken.None));
        Assert.Equal(MotivoImagemInvalida.Formato, erro.Motivo);
    }

    [Fact]
    public async Task RejeitaAnimacaoDimensoesExcessivasEEntradaGrande()
    {
        var processador = new ProcessadorImagens();
        using var animado = new MemoryStream(Convert.FromBase64String(PngAnimado));
        using var largo = new MemoryStream(Convert.FromBase64String(PngLargo));
        using var grande = new MemoryStream(new byte[LimitesImagem.EntradaBytes + 1]);
        Assert.Equal(MotivoImagemInvalida.Formato, (await Assert.ThrowsAsync<ImagemInvalidaException>(() =>
            processador.OtimizarAsync(animado, "animado.png", "image/png", CancellationToken.None))).Motivo);
        Assert.Equal(MotivoImagemInvalida.Dimensoes, (await Assert.ThrowsAsync<ImagemInvalidaException>(() =>
            processador.OtimizarAsync(largo, "largo.png", "image/png", CancellationToken.None))).Motivo);
        Assert.Equal(MotivoImagemInvalida.TamanhoEntrada, (await Assert.ThrowsAsync<ImagemInvalidaException>(() =>
            processador.OtimizarAsync(grande, "grande.png", "image/png", CancellationToken.None))).Motivo);
    }

    [Fact]
    public async Task RejeitaPngTruncado()
    {
        var dados = Convert.FromBase64String(PngTransparente);
        using var origem = new MemoryStream(dados[..^10]);
        var erro = await Assert.ThrowsAsync<ImagemInvalidaException>(() =>
            new ProcessadorImagens().OtimizarAsync(origem, "truncado.png", "image/png", CancellationToken.None));
        Assert.Equal(MotivoImagemInvalida.Conteudo, erro.Motivo);
    }

    [PostgreSqlFact]
    public async Task ArmazenamentoCompensaFalhaELimpaApenasOrfaosAntigos()
    {
        var diretorio = Directory.CreateTempSubdirectory("artgoncalense-imagens-");
        try
        {
            var armazenamento = new ArmazenamentoImagensLocal(diretorio.FullName);
            using var origem = new MemoryStream(Convert.FromBase64String(PngTransparente));
            var imagem = await new ProcessadorImagens().OtimizarAsync(origem, "../imagem.png", "image/png", CancellationToken.None);
            var vinculada = await armazenamento.SalvarAsync(imagem, CancellationToken.None);
            var avatar = await armazenamento.SalvarAsync(imagem, CancellationToken.None);
            var orfa = await armazenamento.SalvarAsync(imagem, CancellationToken.None);
            var recente = await armazenamento.SalvarAsync(imagem, CancellationToken.None);
            var compensada = await armazenamento.SalvarAsync(imagem, CancellationToken.None);
            Assert.NotEqual(vinculada, orfa);
            await Assert.ThrowsAsync<ArgumentException>(() => armazenamento.AbrirAsync("../imagem.webp", CancellationToken.None));
            await armazenamento.ExcluirAsync(compensada, CancellationToken.None);
            await armazenamento.ExcluirAsync(compensada, CancellationToken.None);
            Assert.Null(await armazenamento.AbrirAsync(compensada, CancellationToken.None));

            var temporario = Path.Combine(diretorio.FullName, Guid.NewGuid().ToString("N") + ".webp.tmp");
            await File.WriteAllBytesAsync(temporario, [1]);
            foreach (var arquivo in new[] { Path.Combine(diretorio.FullName, vinculada),
                Path.Combine(diretorio.FullName, avatar), Path.Combine(diretorio.FullName, orfa), temporario })
            {
                File.SetLastWriteTimeUtc(arquivo, DateTime.UtcNow.AddHours(-25));
            }

            var options = new DbContextOptionsBuilder<ArtGoncalenseDbContext>()
                .UseNpgsql(Environment.GetEnvironmentVariable(PostgreSqlFactAttribute.ConnectionVariable)!)
                .Options;
            await using var db = new ArtGoncalenseDbContext(options);
            await db.Database.MigrateAsync();
            await using var transacao = await db.Database.BeginTransactionAsync();
            var artista = new Usuario(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.test", "Artista",
                PapelUsuario.Artista, StatusUsuario.Ativo, DateTimeOffset.UtcNow, avatarPath: avatar);
            db.Add(artista);
            db.Add(new Ilustracao(Guid.NewGuid(), artista.Id, "Ilustração", "", CategoriaIlustracao.Original,
                vinculada, vinculada, ImagemOtimizada.Tipo, imagem.Bytes.Length, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();

            Assert.Equal(2, await new ReconciliadorImagens(armazenamento, db, TimeProvider.System)
                .LimparOrfaosAsync(CancellationToken.None));
            Assert.Null(await armazenamento.AbrirAsync(orfa, CancellationToken.None));
            Assert.False(File.Exists(temporario));
            await using var fluxo = await armazenamento.AbrirAsync(vinculada, CancellationToken.None);
            await using var fluxoRecente = await armazenamento.AbrirAsync(recente, CancellationToken.None);
            await using var fluxoAvatar = await armazenamento.AbrirAsync(avatar, CancellationToken.None);
            Assert.NotNull(fluxoRecente);
            Assert.NotNull(fluxoAvatar);
            using var leitura = new MemoryStream();
            await fluxo!.CopyToAsync(leitura);
            Assert.Equal(imagem.Bytes, leitura.ToArray());
        }
        finally
        {
            diretorio.Delete(recursive: true);
        }
    }
}
