using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArtGoncalense.Tests;

public sealed partial class ApiTests
{
    [PostgreSqlFact]
    public async Task LinkApoioLimitaDestinoEPermiteEdicaoSomentePeloTitular()
    {
        using var aplicacao = new Aplicacao();
        using var artista = await aplicacao.CriarClienteAsync();
        using var outro = await aplicacao.CriarClienteAsync();
        using var visitante = await aplicacao.CriarClienteAsync();
        var id = await CadastrarArtistaAsync(artista, "Artista");
        var idOutro = await CadastrarArtistaAsync(outro, "Outro artista");
        const string rota = "/api/artistas/me/apoio";
        const string url = "https://ko-fi.com/Artista_123";
        using (var anonimo = await visitante.PutAsJsonAsync(rota, new { url }))
        using (var semCsrf = await artista.PutAsJsonAsync(rota, new { url }))
        using (var outroId = await EnviarComCsrfAsync(artista, HttpMethod.Put, rota, new { id = idOutro, url }))
        using (var semUrl = await EnviarComCsrfAsync(artista, HttpMethod.Put, rota, new { }))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, semCsrf.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, outroId.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, semUrl.StatusCode);
        }

        string[] invalidos =
        [
            "http://ko-fi.com/artista",
            "https://ko-fi.com.example.test/artista",
            "https://sub.ko-fi.com/artista",
            "https://ko-fi.com@other.example.test/artista",
            "https://ko-fi.com:444/artista",
            "https://ko-fi.com/artista?redirect=https://other.example.test",
            "https://ko-fi.com/artista#destino",
            "https://ko-fi.com/artista/shop",
            "https://ko-fi.com/login",
            "https://ko-fi.com/%61rtista",
            " "
        ];
        foreach (var invalido in invalidos)
        {
            using var resposta = await EnviarComCsrfAsync(artista, HttpMethod.Put, rota, new { url = invalido });
            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        }

        using var cadastro = await EnviarComCsrfAsync(artista, HttpMethod.Put, rota, new { url = url + "/" });
        Assert.Equal(HttpStatusCode.NoContent, cadastro.StatusCode);
        using var perfil = await visitante.GetFromJsonAsync<JsonDocument>($"/api/artistas/{id}");
        var apoio = perfil!.RootElement.GetProperty("apoio");
        Assert.Equal("Ko-fi", apoio.GetProperty("provedor").GetString());
        Assert.Equal(url, apoio.GetProperty("url").GetString());
        using var perfilOutro = await visitante.GetFromJsonAsync<JsonDocument>($"/api/artistas/{idOutro}");
        Assert.False(perfilOutro!.RootElement.TryGetProperty("apoio", out _));

        var edicoes = await Task.WhenAll(
            EnviarComCsrfAsync(artista, HttpMethod.Put, rota, new { url = "https://ko-fi.com/artista2" }),
            EnviarComCsrfAsync(artista, HttpMethod.Put, rota, new { url = "https://ko-fi.com/artista3" }));
        using var primeira = edicoes[0];
        using var segunda = edicoes[1];
        Assert.All(edicoes, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));
        await using var scope = aplicacao.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        Assert.Equal(1, await db.LinksApoio.CountAsync(x => x.UsuarioId == id));

        using var removido = await EnviarComCsrfAsync(artista, HttpMethod.Put, rota, new { url = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, removido.StatusCode);
        Assert.False(await db.LinksApoio.AnyAsync(x => x.UsuarioId == id));

        db.LinksApoio.Add(new LinkApoio(Guid.NewGuid(), id, "https://outro.example.test/artista", true));
        await db.SaveChangesAsync();
        using var legado = await visitante.GetFromJsonAsync<JsonDocument>($"/api/artistas/{id}");
        Assert.False(legado!.RootElement.TryGetProperty("apoio", out _));
    }
}
