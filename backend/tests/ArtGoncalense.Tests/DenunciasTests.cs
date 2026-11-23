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
    public async Task DenunciasProtegemContatoEPermitemEncerramentoAuditado()
    {
        using var aplicacao = new Aplicacao();
        using var artista = await aplicacao.CriarClienteAsync();
        using var visitante = await aplicacao.CriarClienteAsync();
        using var moderador = await aplicacao.CriarClienteAsync();

        var (_, email) = await CriarSessaoParaPublicarAsync(artista);
        await ConfirmarArtistaAsync(aplicacao, artista, email);
        var aprovada = await CriarIlustracaoPendenteAsync(artista);
        var pendente = await CriarIlustracaoPendenteAsync(artista);
        var moderadorId = await CriarSessaoModeracaoAsync(aplicacao, moderador, PapelUsuario.Moderador);
        using var aprovacao = await DecidirAsync(moderador, aprovada, "APROVAR");
        Assert.Equal(HttpStatusCode.NoContent, aprovacao.StatusCode);

        await ObterCsrfAsync(visitante);
        using (var semCsrf = await visitante.PostAsJsonAsync($"/api/ilustracoes/{aprovada}/denuncias",
            new { motivo = "OUTRO", descricao = "Descrição de teste" }))
        using (var privada = await DenunciarAsync(visitante, pendente))
        using (var semContato = await DenunciarAsync(visitante, aprovada, motivo: "DIREITOS_AUTORAIS"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, semCsrf.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, privada.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, semContato.StatusCode);
        }

        const string contato = "titular@example.test";
        using (var comum = await DenunciarAsync(visitante, aprovada))
        using (var direitos = await DenunciarAsync(visitante, aprovada, "DIREITOS_AUTORAIS", contato))
        {
            Assert.Equal(HttpStatusCode.Accepted, comum.StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, direitos.StatusCode);
            Assert.Empty(await comum.Content.ReadAsStringAsync());
            Assert.Empty(await direitos.Content.ReadAsStringAsync());
        }

        const string rotaFila = "/api/moderacao/denuncias?pageSize=100";
        using (var semSessao = await visitante.GetAsync(rotaFila))
        using (var semPapel = await artista.GetAsync(rotaFila))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, semSessao.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, semPapel.StatusCode);
        }

        using var fila = await moderador.GetAsync(rotaFila);
        Assert.Equal(HttpStatusCode.OK, fila.StatusCode);
        Assert.True(fila.Headers.CacheControl?.NoStore);
        using var dadosFila = JsonDocument.Parse(await fila.Content.ReadAsStringAsync());
        var itens = dadosFila.RootElement.GetProperty("itens").EnumerateArray()
            .Where(x => x.GetProperty("ilustracaoId").GetGuid() == aprovada).ToArray();
        Assert.Equal(2, itens.Length);
        var direitosAutorais = Assert.Single(itens, x => x.GetProperty("motivo").GetString() == "DIREITOS_AUTORAIS");
        Assert.Equal(contato, direitosAutorais.GetProperty("contato").GetString());
        Assert.Equal("Descrição de teste", direitosAutorais.GetProperty("descricao").GetString());
        var comumRecebida = Assert.Single(itens, x => x.GetProperty("motivo").GetString() == "OUTRO");
        Assert.Equal(JsonValueKind.Null, comumRecebida.GetProperty("contato").ValueKind);
        var denunciaId = direitosAutorais.GetProperty("id").GetGuid();
        var rotaEncerrar = $"/api/moderacao/denuncias/{denunciaId}/encerrar";

        using (var semPapel = await EnviarComCsrfAsync(artista, HttpMethod.Post, rotaEncerrar,
            new { justificativa = "Tentativa do artista" }))
        using (var semCsrf = await moderador.PostAsJsonAsync(rotaEncerrar, new { justificativa = "Revisão" }))
        using (var invalida = await EnviarComCsrfAsync(moderador, HttpMethod.Post, rotaEncerrar,
            new { justificativa = " " }))
        {
            Assert.Equal(HttpStatusCode.Forbidden, semPapel.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, semCsrf.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        }

        var encerramentos = await Task.WhenAll(
            EnviarComCsrfAsync(moderador, HttpMethod.Post, rotaEncerrar, new { justificativa = "Revisão um" }),
            EnviarComCsrfAsync(moderador, HttpMethod.Post, rotaEncerrar, new { justificativa = "Revisão dois" }));
        using var primeiro = encerramentos[0];
        using var segundo = encerramentos[1];
        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict },
            encerramentos.Select(resultado => resultado.StatusCode).Order().ToArray());

        using var filaAtualizada = await moderador.GetFromJsonAsync<JsonDocument>(rotaFila);
        Assert.DoesNotContain(filaAtualizada!.RootElement.GetProperty("itens").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == denunciaId);

        await using var scope = aplicacao.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        var encerrada = await db.Denuncias.AsNoTracking().SingleAsync(x => x.Id == denunciaId);
        Assert.Equal(Denuncia.Encerrada, encerrada.Status);
        Assert.Equal(moderadorId, encerrada.EncerradaPorId);
        Assert.True(encerrada.EncerradaEm >= encerrada.CriadoEm);
        Assert.Equal(primeiro.StatusCode == HttpStatusCode.NoContent ? "Revisão um" : "Revisão dois",
            encerrada.JustificativaEncerramento);
        Assert.Equal(StatusIlustracao.Aprovada,
            (await db.Ilustracoes.AsNoTracking().SingleAsync(x => x.Id == aprovada)).Status);
        Assert.False(await db.Denuncias.AnyAsync(x => x.IlustracaoId == pendente));
    }

    [PostgreSqlFact]
    public async Task LimiteDeDenunciasPorIpValeEntreIlustracoes()
    {
        using var aplicacao = new Aplicacao();
        using var artista = await aplicacao.CriarClienteAsync();
        using var visitante = await aplicacao.CriarClienteAsync();
        using var admin = await aplicacao.CriarClienteAsync();
        var (_, email) = await CriarSessaoParaPublicarAsync(artista);
        await ConfirmarArtistaAsync(aplicacao, artista, email);
        var primeira = await CriarIlustracaoPendenteAsync(artista);
        var segunda = await CriarIlustracaoPendenteAsync(artista);
        await CriarSessaoModeracaoAsync(aplicacao, admin, PapelUsuario.Admin);
        using var aprovacaoPrimeira = await DecidirAsync(admin, primeira, "APROVAR");
        using var aprovacaoSegunda = await DecidirAsync(admin, segunda, "APROVAR");
        Assert.Equal(HttpStatusCode.NoContent, aprovacaoPrimeira.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, aprovacaoSegunda.StatusCode);

        await ObterCsrfAsync(visitante);
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            using var aceita = await DenunciarAsync(visitante, primeira);
            Assert.Equal(HttpStatusCode.Accepted, aceita.StatusCode);
        }

        using var excedente = await DenunciarAsync(visitante, segunda);
        Assert.Equal(HttpStatusCode.TooManyRequests, excedente.StatusCode);
        using var dados = await excedente.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(429, dados!.RootElement.GetProperty("status").GetInt32());

        await using var scope = aplicacao.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        Assert.Equal(5, await db.Denuncias.CountAsync(x => x.IlustracaoId == primeira));
        Assert.False(await db.Denuncias.AnyAsync(x => x.IlustracaoId == segunda));
    }

    private static Task<HttpResponseMessage> DenunciarAsync(HttpClient cliente, Guid id,
        string motivo = "OUTRO", string? contato = null) => EnviarComCsrfAsync(cliente, HttpMethod.Post,
            $"/api/ilustracoes/{id}/denuncias", new { motivo, descricao = "Descrição de teste", contato });
}
