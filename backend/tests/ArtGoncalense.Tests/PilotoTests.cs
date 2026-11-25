using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ArtGoncalense.API;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArtGoncalense.Tests;

public sealed partial class ApiTests
{
    [PostgreSqlFact]
    public async Task SessaoECodigoContinuamValidosDepoisDeReiniciarComChavesProtegidas()
    {
        using var rsa = RSA.Create(2048);
        var solicitacao = new CertificateRequest("CN=ArtGoncalense.Teste", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificado = solicitacao.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        var pfx = Convert.ToBase64String(certificado.Export(X509ContentType.Pkcs12));
        var email = $"{Guid.NewGuid():N}@example.test";
        var opcoes = new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        };

        using var aplicacao = new Aplicacao();
        using (await aplicacao.CriarClienteAsync()) { }
        string cookie;
        using (var primeira = aplicacao.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AdicionarProtecaoPiloto(pfx))))
        using (var navegador = primeira.CreateClient(opcoes))
        {
            await ObterCsrfAsync(navegador);
            using var cadastro = await PostAsync(navegador, "registrar",
                new { email, senha = Senha, nomePublico = "Artista", maiorDeIdade = true });
            Assert.Equal(HttpStatusCode.Accepted, cadastro.StatusCode);
            using var login = await PostAsync(navegador, "login", new { email, senha = Senha });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"),
                valor => valor.StartsWith("__Host-ArtGoncalense=", StringComparison.Ordinal)).Split(';', 2)[0];
        }

        var codigo = Assert.Single(aplicacao.Mensagens("confirmacao"));
        using var segunda = aplicacao.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AdicionarProtecaoPiloto(pfx)));
        using var aposReinicio = segunda.CreateClient(opcoes);
        aposReinicio.DefaultRequestHeaders.Add("Cookie", cookie);
        using var sessao = await aposReinicio.GetAsync("/api/contas/sessao");
        Assert.Equal(HttpStatusCode.OK, sessao.StatusCode);
        await ObterCsrfAsync(aposReinicio);
        using var confirmacao = await PostAsync(aposReinicio, "confirmar-email", new { email, codigo });
        Assert.Equal(HttpStatusCode.NoContent, confirmacao.StatusCode);

        await using var escopo = segunda.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        var xml = await db.ChavesProtecao.Select(chave => chave.Xml).ToArrayAsync();
        Assert.NotEmpty(xml);
        Assert.All(xml, chave => Assert.Contains("encryptedSecret", chave, StringComparison.Ordinal));
    }

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
