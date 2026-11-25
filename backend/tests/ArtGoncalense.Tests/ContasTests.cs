using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtGoncalense.Application.Contas;
using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Imagens;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArtGoncalense.Tests;

[Collection("PostgreSQL local")]
public sealed partial class ApiTests
{
    private const string Senha = "frase de teste longa";
    private const string NovaSenha = "outra frase de teste longa";

    [PostgreSqlFact]
    public async Task CadastroConfirmacaoRecuperacaoELogoutProtegemASessao()
    {
        using var aplicacao = new Aplicacao();
        using var cliente = await aplicacao.CriarClienteAsync();
        var email = $"{Guid.NewGuid():N}@example.test";
        var cadastro = new { email, senha = Senha, nomePublico = "Artista de teste", maiorDeIdade = true };

        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/contas/registrar", cadastro)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/contas/sessao")).StatusCode);
        await ObterCsrfAsync(cliente);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostAsync(cliente, "registrar", new { email, senha = "curta", cadastro.nomePublico, cadastro.maiorDeIdade })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostAsync(cliente, "registrar", new { email = "invalido", senha = Senha, cadastro.nomePublico, cadastro.maiorDeIdade })).StatusCode);

        var novo = await PostAsync(cliente, "registrar", cadastro);
        var repetido = await PostAsync(cliente, "registrar", cadastro with { email = email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Accepted, novo.StatusCode);
        Assert.Equal(novo.StatusCode, repetido.StatusCode);
        Assert.Equal(await novo.Content.ReadAsStringAsync(), await repetido.Content.ReadAsStringAsync());
        var confirmacao = Assert.Single(aplicacao.Mensagens("confirmacao"));

        var login = await PostAsync(cliente, "login", new { email, senha = Senha });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"),
            valor => valor.StartsWith("__Host-ArtGoncalense=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/testes/permissao-publicar")).StatusCode);
        await ObterCsrfAsync(cliente);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostAsync(cliente, "confirmar-email", new { email, codigo = "invalido" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/testes/permissao-publicar")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await PostAsync(cliente, "confirmar-email", new { email, codigo = confirmacao })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.GetAsync("/testes/permissao-publicar")).StatusCode);

        var desconhecido = await PostAsync(cliente, "solicitar-recuperacao", new { email = "ausente@example.test" });
        var existente = await PostAsync(cliente, "solicitar-recuperacao", new { email });
        Assert.Equal(HttpStatusCode.Accepted, existente.StatusCode);
        Assert.Equal(existente.StatusCode, desconhecido.StatusCode);
        Assert.Equal(await existente.Content.ReadAsStringAsync(), await desconhecido.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(cliente, "solicitar-recuperacao", new { email })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(cliente, "solicitar-recuperacao", new { email })).StatusCode);
        var codigos = aplicacao.Mensagens("recuperacao");
        Assert.Equal(2, codigos.Count);

        var redefinicao = new { email, codigo = codigos[0], senha = NovaSenha };
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(cliente, "redefinir-senha", redefinicao)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/contas/sessao")).StatusCode);
        await ObterCsrfAsync(cliente);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(cliente, "redefinir-senha", redefinicao)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(cliente, "login", new { email, senha = Senha })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(cliente, "login", new { email, senha = NovaSenha })).StatusCode);
        await ObterCsrfAsync(cliente);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/contas/sair", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(cliente, "sair", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/contas/sessao")).StatusCode);
    }

    [PostgreSqlFact]
    public async Task LimitesBloqueiamTentativasSemRevelarConta()
    {
        using var aplicacao = new Aplicacao();
        using var cliente = await aplicacao.CriarClienteAsync();
        await ObterCsrfAsync(cliente);
        var email = $"{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Accepted,
            (await PostAsync(cliente, "registrar", new { email, senha = Senha, nomePublico = "Artista", maiorDeIdade = true })).StatusCode);

        var ausente = await PostAsync(cliente, "login", new { email = "ausente@example.test", senha = Senha });
        Assert.Equal(HttpStatusCode.Unauthorized, ausente.StatusCode);
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            var rejeitado = await PostAsync(cliente, "login", new { email, senha = "senha errada" });
            Assert.Equal(ausente.StatusCode, rejeitado.StatusCode);
            var corpo = await rejeitado.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal("E-mail ou senha inválidos.", corpo?.Title);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(cliente, "login", new { email, senha = Senha })).StatusCode);
        for (var tentativa = 7; tentativa < 20; tentativa++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(cliente, "login", new { email, senha = Senha })).StatusCode);
        }

        using var limitado = await PostAsync(cliente, "login", new { email, senha = Senha });
        Assert.Equal(HttpStatusCode.TooManyRequests, limitado.StatusCode);
        var problema = await limitado.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.True(problema?.Extensions.ContainsKey("traceId"));
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(cliente, "reenviar-confirmacao", new { email })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync(cliente, "reenviar-confirmacao", new { email })).StatusCode);
        Assert.Equal(3, aplicacao.Mensagens("confirmacao").Count);
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(cliente, "solicitar-recuperacao", new { email })).StatusCode);
    }

    [PostgreSqlFact]
    public async Task PerfilPublicoOmiteDadosPrivadosEEdicaoFicaRestritaAoTitular()
    {
        using var aplicacao = new Aplicacao();
        using var visitante = await aplicacao.CriarClienteAsync();
        using var primeiro = await aplicacao.CriarClienteAsync();
        using var segundo = await aplicacao.CriarClienteAsync();
        const string rota = "/api/artistas/me";

        var primeiroId = await CadastrarArtistaAsync(primeiro, "Primeiro artista");
        var segundoId = await CadastrarArtistaAsync(segundo, "Segundo artista");
        var outroPerfil = $"/api/artistas/{segundoId}";
        var dados = new { nomePublico = "  Nome atualizado  ", bio = "  Biografia pública  " };

        Assert.Equal(HttpStatusCode.Unauthorized, (await visitante.PutAsJsonAsync(rota, dados)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await primeiro.PutAsJsonAsync(rota, dados)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await EnviarComCsrfAsync(primeiro, HttpMethod.Put, outroPerfil, dados)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarComCsrfAsync(primeiro, HttpMethod.Put, rota,
            new { id = segundoId, dados.nomePublico, dados.bio })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarComCsrfAsync(primeiro, HttpMethod.Put, rota,
            new { nomePublico = " ", bio = new string('x', 2001) })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await EnviarComCsrfAsync(primeiro, HttpMethod.Put, rota, dados)).StatusCode);
        using var resposta = await visitante.GetAsync($"/api/artistas/{primeiroId}");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var perfil = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var publico = perfil.RootElement;
        Assert.Equal(new[] { "bio", "id", "nomePublico" },
            publico.EnumerateObject().Select(campo => campo.Name).Order().ToArray());
        Assert.Equal(primeiroId, publico.GetProperty("id").GetGuid());
        Assert.Equal("Nome atualizado", publico.GetProperty("nomePublico").GetString());
        Assert.Equal("Biografia pública", publico.GetProperty("bio").GetString());

        using var respostaSegundo = await visitante.GetAsync(outroPerfil);
        Assert.Equal(HttpStatusCode.OK, respostaSegundo.StatusCode);
        using var perfilSegundo = JsonDocument.Parse(await respostaSegundo.Content.ReadAsStringAsync());
        Assert.Equal("Segundo artista", perfilSegundo.RootElement.GetProperty("nomePublico").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await EnviarComCsrfAsync(primeiro, HttpMethod.Put, rota,
            new { nomePublico = "Nome atualizado", bio = (string?)null })).StatusCode);
        using var semBio = await visitante.GetFromJsonAsync<JsonDocument>($"/api/artistas/{primeiroId}");
        Assert.Equal(JsonValueKind.Null, semBio!.RootElement.GetProperty("bio").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await visitante.GetAsync($"/api/artistas/{Guid.NewGuid()}")).StatusCode);

        await using var scope = aplicacao.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        var suspenso = new Usuario(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.test", "Artista suspenso",
            PapelUsuario.Artista, StatusUsuario.Suspenso, DateTimeOffset.UtcNow);
        db.Usuarios.Add(suspenso);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await visitante.GetAsync($"/api/artistas/{suspenso.Id}")).StatusCode);
    }

    [PostgreSqlFact]
    public async Task CadastroExigeDeclaracaoDeMaioridade()
    {
        using var aplicacao = new Aplicacao();
        using var cliente = await aplicacao.CriarClienteAsync();
        await ObterCsrfAsync(cliente);
        var email = $"{Guid.NewGuid():N}@example.test";
        var dados = new { email, senha = Senha, nomePublico = "Artista" };
        using var ausente = await PostAsync(cliente, "registrar", dados);
        using var negativa = await PostAsync(cliente, "registrar",
            new { dados.email, dados.senha, dados.nomePublico, maiorDeIdade = false });
        Assert.Equal(HttpStatusCode.BadRequest, ausente.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negativa.StatusCode);
        Assert.Empty(aplicacao.Mensagens("confirmacao"));
        await using var scope = aplicacao.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        Assert.False(await db.Usuarios.AnyAsync(x => x.Email == email));
        using var aceita = await PostAsync(cliente, "registrar",
            new { dados.email, dados.senha, dados.nomePublico, maiorDeIdade = true });
        Assert.Equal(HttpStatusCode.Accepted, aceita.StatusCode);
        Assert.Single(aplicacao.Mensagens("confirmacao"));
    }

    private static async Task<Guid> CadastrarArtistaAsync(HttpClient cliente, string nomePublico)
    {
        await ObterCsrfAsync(cliente);
        var email = $"{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Accepted,
            (await PostAsync(cliente, "registrar", new { email, senha = Senha, nomePublico, maiorDeIdade = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await PostAsync(cliente, "login", new { email, senha = Senha })).StatusCode);
        await ObterCsrfAsync(cliente);
        using var sessao = await cliente.GetFromJsonAsync<JsonDocument>("/api/contas/sessao");
        return sessao!.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task ObterCsrfAsync(HttpClient cliente)
    {
        using var resposta = await cliente.GetAsync("/api/contas/csrf");
        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var cookie = Assert.Single(resposta.Headers.GetValues("Set-Cookie"),
            valor => valor.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        var token = cookie.Split(';', 2)[0]["XSRF-TOKEN=".Length..];
        TokensCsrf.AddOrUpdate(cliente, token);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HttpClient, string> TokensCsrf = new();

    private static Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rota, object dados) =>
        EnviarComCsrfAsync(cliente, HttpMethod.Post, $"/api/contas/{rota}", dados);

    private static async Task<HttpResponseMessage> EnviarComCsrfAsync(
        HttpClient cliente, HttpMethod metodo, string rota, object dados)
    {
        using var pedido = new HttpRequestMessage(metodo, rota)
        {
            Content = JsonContent.Create(dados)
        };
        Assert.True(TokensCsrf.TryGetValue(cliente, out var token));
        pedido.Headers.Add("X-XSRF-TOKEN", token);
        return await cliente.SendAsync(pedido);
    }

    private sealed class Aplicacao(bool sincronizarUploads = false) : WebApplicationFactory<Program>
    {
        private readonly DirectoryInfo correio = Directory.CreateTempSubdirectory("artgoncalense-contas-");
        private readonly DirectoryInfo imagens = Directory.CreateTempSubdirectory("artgoncalense-publicacoes-");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ArtGoncalense"] = Environment.GetEnvironmentVariable(PostgreSqlFactAttribute.ConnectionVariable),
                    ["Contas:DiretorioEmailLocal"] = correio.FullName,
                    ["Imagens:DiretorioLocal"] = imagens.FullName
                }));
            builder.ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(ApiTests).Assembly);
                if (sincronizarUploads)
                {
                    services.AddSingleton<IArmazenamentoImagens>(provider =>
                        new ArmazenamentoSincronizado(provider.GetRequiredService<ArmazenamentoImagensLocal>()));
                }
            });
        }

        public async Task<HttpClient> CriarClienteAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
            await db.Database.MigrateAsync();
            return CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        }

        public IReadOnlyList<string> Mensagens(string finalidade)
        {
            var codigos = new List<string>();
            foreach (var arquivo in correio.EnumerateFiles("*.json"))
            {
                using var mensagem = JsonDocument.Parse(File.ReadAllText(arquivo.FullName));
                if (mensagem.RootElement.GetProperty("finalidade").GetString() == finalidade)
                {
                    codigos.Add(mensagem.RootElement.GetProperty("codigo").GetString()!);
                }
            }

            return codigos;
        }

        public string[] ImagensArmazenadas() => imagens.EnumerateFiles().Select(arquivo => arquivo.Name).ToArray();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && correio.Exists)
            {
                correio.Delete(recursive: true);
            }

            if (disposing && imagens.Exists)
            {
                imagens.Delete(recursive: true);
            }
        }
    }
}

[ApiController]
[Route("testes/permissao-publicar")]
public sealed class PermissaoPublicarTestsController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AcessoConta.PublicarIlustracao)]
    public IActionResult Verificar() => NoContent();
}
