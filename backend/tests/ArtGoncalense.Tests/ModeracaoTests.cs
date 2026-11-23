using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Identidade;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArtGoncalense.Tests;

public sealed partial class ApiTests
{
    [PostgreSqlFact]
    public async Task ModeracaoRestringeAcessoERegistraAprovacaoRejeicaoEOcultacao()
    {
        using var aplicacao = new Aplicacao();
        using var artista = await aplicacao.CriarClienteAsync();
        using var moderador = await aplicacao.CriarClienteAsync();
        using var visitante = await aplicacao.CriarClienteAsync();

        var (artistaId, email) = await CriarSessaoParaPublicarAsync(artista);
        await ConfirmarArtistaAsync(aplicacao, artista, email);
        var primeira = await CriarIlustracaoPendenteAsync(artista);
        var segunda = await CriarIlustracaoPendenteAsync(artista);
        var moderadorId = await CriarSessaoModeracaoAsync(aplicacao, moderador, PapelUsuario.Moderador);
        var imagemUrl = $"/api/moderacao/{primeira}/imagem";

        using (var semSessao = await visitante.GetAsync("/api/moderacao/pendentes"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, semSessao.StatusCode);
        }

        using (var filaArtista = await artista.GetAsync("/api/moderacao/pendentes"))
        using (var imagemArtista = await artista.GetAsync(imagemUrl))
        using (var decisaoArtista = await DecidirAsync(artista, primeira, "APROVAR"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, filaArtista.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, imagemArtista.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, decisaoArtista.StatusCode);
        }

        using var fila = await moderador.GetFromJsonAsync<JsonDocument>("/api/moderacao/pendentes?page=1&pageSize=100");
        var item = Assert.Single(fila!.RootElement.GetProperty("itens").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == primeira);
        Assert.Equal(artistaId, item.GetProperty("artistaId").GetGuid());
        Assert.Equal(imagemUrl, item.GetProperty("imagemUrl").GetString());
        Assert.False(item.TryGetProperty("email", out _));
        Assert.False(item.TryGetProperty("caminhoStorage", out _));

        using (var imagem = await moderador.GetAsync(imagemUrl))
        {
            Assert.Equal(HttpStatusCode.OK, imagem.StatusCode);
            Assert.Equal("image/webp", imagem.Content.Headers.ContentType?.MediaType);
            Assert.Equal("nosniff", Assert.Single(imagem.Headers.GetValues("X-Content-Type-Options")));
            Assert.True(imagem.Headers.CacheControl?.NoStore);
        }

        using (var paginaInvalida = await moderador.GetAsync("/api/moderacao/pendentes?pageSize=101"))
        using (var semCsrf = await moderador.PostAsJsonAsync($"/api/moderacao/{primeira}/decisao",
            new { acao = "APROVAR", justificativa = "Revisada" }))
        using (var semJustificativa = await DecidirAsync(moderador, primeira, "APROVAR", " "))
        using (var estadoInvalido = await DecidirAsync(moderador, primeira, "OCULTAR"))
        using (var inexistente = await DecidirAsync(moderador, Guid.NewGuid(), "APROVAR"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, paginaInvalida.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, semCsrf.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, semJustificativa.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, estadoInvalido.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        }

        await using var scope = aplicacao.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        Assert.False(await db.AcoesModeracao.AnyAsync(x => x.IlustracaoId == primeira));

        using (var aprovada = await DecidirAsync(moderador, primeira, "APROVAR", "  Revisão de teste  "))
        using (var repetida = await DecidirAsync(moderador, primeira, "REJEITAR"))
        using (var oculta = await DecidirAsync(moderador, primeira, "OCULTAR"))
        using (var rejeitada = await DecidirAsync(moderador, segunda, "REJEITAR"))
        using (var reabertura = await DecidirAsync(moderador, segunda, "APROVAR"))
        using (var imagemOculta = await moderador.GetAsync(imagemUrl))
        {
            Assert.Equal(HttpStatusCode.NoContent, aprovada.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, oculta.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, rejeitada.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, reabertura.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, imagemOculta.StatusCode);
        }

        using var filaAtualizada = await moderador.GetFromJsonAsync<JsonDocument>("/api/moderacao/pendentes?pageSize=100");
        Assert.DoesNotContain(filaAtualizada!.RootElement.GetProperty("itens").EnumerateArray(), item =>
            item.GetProperty("id").GetGuid() == primeira || item.GetProperty("id").GetGuid() == segunda);
        var acoes = await db.AcoesModeracao.AsNoTracking().Where(x => x.IlustracaoId == primeira || x.IlustracaoId == segunda)
            .ToListAsync();
        Assert.Equal(3, acoes.Count);
        Assert.All(acoes, registro => Assert.Equal(moderadorId, registro.ModeradorId));
        Assert.Equal("Revisão de teste", Assert.Single(acoes, x => x.Acao == TipoAcaoModeracao.Aprovar).Justificativa);
        var estadoPrimeira = await db.Ilustracoes.AsNoTracking().SingleAsync(x => x.Id == primeira);
        var estadoSegunda = await db.Ilustracoes.AsNoTracking().SingleAsync(x => x.Id == segunda);
        Assert.Equal(StatusIlustracao.Oculta, estadoPrimeira.Status);
        Assert.Equal(StatusIlustracao.Rejeitada, estadoSegunda.Status);
        Assert.Equal(Assert.Single(acoes, x => x.Acao == TipoAcaoModeracao.Ocultar).CriadoEm, estadoPrimeira.AtualizadoEm);
    }

    [PostgreSqlFact]
    public async Task DecisoesConcorrentesAplicamUmaUnicaMudancaComAuditoria()
    {
        using var aplicacao = new Aplicacao();
        using var artista = await aplicacao.CriarClienteAsync();
        using var moderador = await aplicacao.CriarClienteAsync();
        using var admin = await aplicacao.CriarClienteAsync();
        var (_, email) = await CriarSessaoParaPublicarAsync(artista);
        await ConfirmarArtistaAsync(aplicacao, artista, email);
        var id = await CriarIlustracaoPendenteAsync(artista);
        var moderadorId = await CriarSessaoModeracaoAsync(aplicacao, moderador, PapelUsuario.Moderador);
        var adminId = await CriarSessaoModeracaoAsync(aplicacao, admin, PapelUsuario.Admin);

        var respostas = await Task.WhenAll(DecidirAsync(moderador, id, "APROVAR"), DecidirAsync(admin, id, "REJEITAR"));
        using var aprovacao = respostas[0];
        using var rejeicao = respostas[1];
        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict },
            respostas.Select(resposta => resposta.StatusCode).Order().ToArray());

        await using var scope = aplicacao.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        var ilustracao = await db.Ilustracoes.AsNoTracking().SingleAsync(x => x.Id == id);
        var registro = await db.AcoesModeracao.AsNoTracking().SingleAsync(x => x.IlustracaoId == id);
        var aprovada = aprovacao.StatusCode == HttpStatusCode.NoContent;
        Assert.Equal(aprovada ? StatusIlustracao.Aprovada : StatusIlustracao.Rejeitada, ilustracao.Status);
        Assert.Equal(aprovada ? TipoAcaoModeracao.Aprovar : TipoAcaoModeracao.Rejeitar, registro.Acao);
        Assert.Equal(aprovada ? moderadorId : adminId, registro.ModeradorId);
        Assert.Equal(ilustracao.AtualizadoEm, registro.CriadoEm);
    }

    private static async Task<Guid> CriarIlustracaoPendenteAsync(HttpClient artista)
    {
        using var resposta = await EnviarIlustracaoAsync(artista);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        using var dados = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return dados.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CriarSessaoModeracaoAsync(Aplicacao aplicacao, HttpClient cliente, PapelUsuario papel)
    {
        await using var scope = aplicacao.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        var gerenciador = scope.ServiceProvider.GetRequiredService<UserManager<ContaIdentity>>();
        var id = Guid.NewGuid();
        var email = $"{id:N}@example.test";
        db.Usuarios.Add(new Usuario(id, email, "Pessoa moderadora", papel, StatusUsuario.Ativo, DateTimeOffset.UtcNow));
        var conta = new ContaIdentity { Id = id, UserName = id.ToString("N"), Email = email, EmailConfirmed = true };
        Assert.True((await gerenciador.CreateAsync(conta, Senha)).Succeeded);
        await ObterCsrfAsync(cliente);
        using var login = await PostAsync(cliente, "login", new { email, senha = Senha });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await ObterCsrfAsync(cliente);
        return id;
    }

    private static Task<HttpResponseMessage> DecidirAsync(HttpClient cliente, Guid id, string acao,
        string justificativa = "Revisão de teste") =>
        EnviarComCsrfAsync(cliente, HttpMethod.Post, $"/api/moderacao/{id}/decisao", new { acao, justificativa });
}
