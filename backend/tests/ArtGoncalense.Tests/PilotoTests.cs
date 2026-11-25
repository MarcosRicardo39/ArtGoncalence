using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ArtGoncalense.Tests;

public sealed partial class ApiTests
{
    [Fact]
    public async Task PilotoProtegeInterfaceEApiEExigeHttps()
    {
        var senha = Guid.NewGuid().ToString("N");
        using var aplicacao = new Aplicacao();
        using var piloto = aplicacao.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Piloto:Ativo"] = "true",
                    ["Piloto:Senha"] = senha
                })));
        using var navegador = piloto.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var home = await navegador.GetAsync("/");
        Assert.Equal(HttpStatusCode.Unauthorized, home.StatusCode);
        Assert.Contains(home.Headers.WwwAuthenticate, x => x.Scheme == "Basic");
        using var cadastro = await navegador.PostAsync("/api/contas/registrar", null);
        Assert.Equal(HttpStatusCode.Unauthorized, cadastro.StatusCode);

        navegador.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"piloto:{senha}")));
        using var csrf = await navegador.GetAsync("/api/contas/csrf");
        Assert.Equal(HttpStatusCode.NoContent, csrf.StatusCode);
        using var rotaInexistente = await navegador.GetAsync("/api/rota-inexistente");
        Assert.Equal(HttpStatusCode.NotFound, rotaInexistente.StatusCode);
        using var semTls = await navegador.GetAsync("http://localhost/api/contas/csrf");
        Assert.Equal(HttpStatusCode.Forbidden, semTls.StatusCode);
    }
}
