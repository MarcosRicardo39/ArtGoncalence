using ArtGoncalense.Domain;

namespace ArtGoncalense.Tests;

public sealed class ModeloInicialTests
{
    private static readonly DateTimeOffset CriadoEm = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData(CategoriaIlustracao.Original)]
    [InlineData(CategoriaIlustracao.Fanart)]
    public void IlustracaoNascePendenteComDatasIguais(CategoriaIlustracao categoria)
    {
        var usuarioId = Guid.NewGuid();

        var ilustracao = CriarIlustracao(usuarioId, categoria, 1200);

        Assert.Equal(usuarioId, ilustracao.UsuarioId);
        Assert.Equal(categoria, ilustracao.Categoria);
        Assert.Equal(StatusIlustracao.Pendente, ilustracao.Status);
        Assert.Equal(CriadoEm, ilustracao.CriadoEm);
        Assert.Equal(ilustracao.CriadoEm, ilustracao.AtualizadoEm);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void IlustracaoRejeitaTamanhoNaoPositivo(long tamanhoBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>("tamanhoBytes",
            () => CriarIlustracao(Guid.NewGuid(), CategoriaIlustracao.Original, tamanhoBytes));
    }

    [Fact]
    public void IlustracaoRejeitaCategoriaDesconhecida()
    {
        Assert.Throws<ArgumentOutOfRangeException>("categoria",
            () => CriarIlustracao(Guid.NewGuid(), (CategoriaIlustracao)999, 1200));
    }

    [Fact]
    public void IlustracaoRejeitaTitularVazio()
    {
        Assert.Throws<ArgumentOutOfRangeException>("usuarioId",
            () => CriarIlustracao(Guid.Empty, CategoriaIlustracao.Fanart, 1200));
    }

    [Fact]
    public void UsuarioRejeitaPapelDesconhecido()
    {
        Assert.Throws<ArgumentOutOfRangeException>("papel",
            () => new Usuario(Guid.NewGuid(), "artista@example.test", "Artista", (PapelUsuario)999,
                StatusUsuario.PendenteConfirmacao, CriadoEm));
    }

    [Fact]
    public void UsuarioRejeitaNomePublicoEmBranco()
    {
        Assert.Throws<ArgumentException>("nomePublico",
            () => new Usuario(Guid.NewGuid(), "artista@example.test", " ", PapelUsuario.Artista,
                StatusUsuario.PendenteConfirmacao, CriadoEm));
    }

    [Fact]
    public void LinkApoioRejeitaTitularVazio()
    {
        Assert.Throws<ArgumentOutOfRangeException>("usuarioId",
            () => new LinkApoio(Guid.NewGuid(), Guid.Empty, "https://example.test/apoio", false));
    }

    [Fact]
    public void DenunciaRejeitaIlustracaoVazia()
    {
        Assert.Throws<ArgumentOutOfRangeException>("ilustracaoId",
            () => new Denuncia(Guid.NewGuid(), Guid.Empty, "Motivo", "Descrição", "Status", CriadoEm));
    }

    [Theory]
    [InlineData(TipoAcaoModeracao.Aprovar)]
    [InlineData(TipoAcaoModeracao.Rejeitar)]
    [InlineData(TipoAcaoModeracao.Ocultar)]
    public void AcaoModeracaoExigeJustificativa(TipoAcaoModeracao acao)
    {
        Assert.Throws<ArgumentException>("justificativa",
            () => new AcaoModeracao(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), acao, " ", CriadoEm));
    }

    [Fact]
    public void AcaoModeracaoExigeIdentidadeDoModerador()
    {
        Assert.Throws<ArgumentOutOfRangeException>("moderadorId",
            () => new AcaoModeracao(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
                TipoAcaoModeracao.Ocultar, "Justificativa", CriadoEm));
    }

    private static Ilustracao CriarIlustracao(Guid usuarioId, CategoriaIlustracao categoria, long tamanhoBytes)
    {
        return new Ilustracao(Guid.NewGuid(), usuarioId, "Ilustração", "Descrição", categoria,
            "ilustracao.png", "ilustracoes/ilustracao.png", "image/png", tamanhoBytes, CriadoEm);
    }
}
