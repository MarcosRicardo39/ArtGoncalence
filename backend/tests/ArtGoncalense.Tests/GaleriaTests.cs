using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtGoncalense.Domain;

namespace ArtGoncalense.Tests;

public sealed partial class ApiTests
{
    [PostgreSqlFact]
    public async Task GaleriaPaginaAprovadasEEscondeImagemAposModeracao()
    {
        using var aplicacao = new Aplicacao();
        using var artista = await aplicacao.CriarClienteAsync();
        using var moderador = await aplicacao.CriarClienteAsync();
        using var visitante = await aplicacao.CriarClienteAsync();
        var (artistaId, email) = await CriarSessaoParaPublicarAsync(artista);
        await ConfirmarArtistaAsync(aplicacao, artista, email);
        var aprovada = await CriarIlustracaoPendenteAsync(artista);
        var pendente = await CriarIlustracaoPendenteAsync(artista);
        var rejeitada = await CriarIlustracaoPendenteAsync(artista);
        await CriarSessaoModeracaoAsync(aplicacao, moderador, PapelUsuario.Moderador);
        using var aprovacao = await DecidirAsync(moderador, aprovada, "APROVAR");
        using var rejeicao = await DecidirAsync(moderador, rejeitada, "REJEITAR");
        Assert.Equal(HttpStatusCode.NoContent, aprovacao.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, rejeicao.StatusCode);

        using var galeria = await visitante.GetFromJsonAsync<JsonDocument>("/api/ilustracoes?pageSize=100");
        var itens = galeria!.RootElement.GetProperty("itens").EnumerateArray();
        Assert.Contains(itens, x => x.GetProperty("id").GetGuid() == aprovada);
        Assert.DoesNotContain(itens, x => x.GetProperty("id").GetGuid() == pendente || x.GetProperty("id").GetGuid() == rejeitada);
        using var pagina = await visitante.GetFromJsonAsync<JsonDocument>("/api/ilustracoes?page=1&pageSize=1");
        Assert.Equal(1, pagina!.RootElement.GetProperty("itens").GetArrayLength());
        Assert.Equal(galeria.RootElement.GetProperty("total").GetInt32(), pagina.RootElement.GetProperty("total").GetInt32());

        using var detalhes = await visitante.GetFromJsonAsync<JsonDocument>($"/api/ilustracoes/{aprovada}");
        Assert.Equal(artistaId, detalhes!.RootElement.GetProperty("artista").GetProperty("id").GetGuid());
        Assert.False(detalhes.RootElement.GetProperty("artista").TryGetProperty("email", out _));
        Assert.False(detalhes.RootElement.TryGetProperty("caminhoStorage", out _));
        var rotaImagem = detalhes.RootElement.GetProperty("imagemUrl").GetString();
        using var imagem = await visitante.GetAsync(rotaImagem);
        Assert.Equal(HttpStatusCode.OK, imagem.StatusCode);
        Assert.Equal("image/webp", imagem.Content.Headers.ContentType?.MediaType);
        Assert.True(imagem.Headers.CacheControl?.NoStore);

        using var ocultacao = await DecidirAsync(moderador, aprovada, "OCULTAR");
        Assert.Equal(HttpStatusCode.NoContent, ocultacao.StatusCode);
        foreach (var privada in new[] { aprovada, pendente, rejeitada })
        {
            using var metadados = await visitante.GetAsync($"/api/ilustracoes/{privada}");
            using var arquivo = await visitante.GetAsync($"/api/ilustracoes/{privada}/imagem");
            Assert.Equal(HttpStatusCode.NotFound, metadados.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, arquivo.StatusCode);
        }
    }
}
