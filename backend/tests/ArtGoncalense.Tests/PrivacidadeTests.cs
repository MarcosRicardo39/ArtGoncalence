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
    public async Task LimpezaDeContatosRespeitaPrazoEConservacaoJustificada()
    {
        using var aplicacao = new Aplicacao();
        using var artista = await aplicacao.CriarClienteAsync();
        using var admin = await aplicacao.CriarClienteAsync();
        var (_, email) = await CriarSessaoParaPublicarAsync(artista);
        await ConfirmarArtistaAsync(aplicacao, artista, email);
        var obra = await CriarIlustracaoPendenteAsync(artista);
        var moderador = await CriarSessaoModeracaoAsync(aplicacao, admin, PapelUsuario.Admin);
        var antiga = Guid.NewGuid();
        var conservada = Guid.NewGuid();
        var recente = Guid.NewGuid();

        await using var escopo = aplicacao.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        var agora = DateTimeOffset.UtcNow;
        db.Denuncias.AddRange(new[] { antiga, conservada, recente }.Select(id =>
            new Denuncia(id, obra, "OUTRO", "Descrição de teste", Denuncia.Recebida,
                agora.AddDays(-100), "contato@example.test")));
        await db.SaveChangesAsync();
        var repositorio = new RepositorioDenuncias(db);
        await repositorio.EncerrarAsync(antiga, moderador, "Revisada", agora.AddDays(-91), default);
        await repositorio.EncerrarAsync(conservada, moderador, "Revisada", agora.AddDays(-91), default);
        await repositorio.EncerrarAsync(recente, moderador, "Revisada", agora.AddDays(-89), default);

        using var conservar = await EnviarComCsrfAsync(admin, HttpMethod.Put,
            $"/api/privacidade/denuncias/{conservada}/conservacao", new { motivo = "Conservação de teste" });
        Assert.Equal(HttpStatusCode.NoContent, conservar.StatusCode);
        using var limpar = await EnviarComCsrfAsync(admin, HttpMethod.Post,
            "/api/privacidade/contatos/limpar", new { });
        Assert.Equal(HttpStatusCode.OK, limpar.StatusCode);
        Assert.Equal(1, (await limpar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("removidos").GetInt32());
        var contatos = await db.Denuncias.AsNoTracking().Where(x => x.IlustracaoId == obra)
            .ToDictionaryAsync(x => x.Id, x => x.Contato);
        Assert.Null(contatos[antiga]);
        Assert.NotNull(contatos[conservada]);
        Assert.NotNull(contatos[recente]);

        using var pedido = await EnviarComCsrfAsync(artista, HttpMethod.Post, "/api/privacidade/exclusao",
            new { senha = Senha, ilustracaoId = obra });
        Assert.Equal(HttpStatusCode.Accepted, pedido.StatusCode);
        var protocolo = (await pedido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var processar = $"/api/privacidade/pedidos/{protocolo}/processar";
        using var bloqueado = await EnviarComCsrfAsync(admin, HttpMethod.Post, processar, new { });
        Assert.Equal(HttpStatusCode.Conflict, bloqueado.StatusCode);
        Assert.Single(aplicacao.ImagensArmazenadas());
        using var liberar = await EnviarComCsrfAsync(admin, HttpMethod.Put,
            $"/api/privacidade/denuncias/{conservada}/conservacao", new { motivo = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, liberar.StatusCode);
        using var concluido = await EnviarComCsrfAsync(admin, HttpMethod.Post, processar, new { });
        Assert.Equal(HttpStatusCode.NoContent, concluido.StatusCode);
        Assert.Empty(aplicacao.ImagensArmazenadas());
    }

    [PostgreSqlFact]
    public async Task ExclusaoConfirmaTitularOcultaDadosEPermiteProcessamentoSomentePorAdmin()
    {
        using var aplicacao = new Aplicacao();
        using var artista = await aplicacao.CriarClienteAsync();
        using var outro = await aplicacao.CriarClienteAsync();
        using var admin = await aplicacao.CriarClienteAsync();
        var (titular, email) = await CriarSessaoParaPublicarAsync(artista);
        await ConfirmarArtistaAsync(aplicacao, artista, email);
        await CriarSessaoParaPublicarAsync(outro);
        var obra = await CriarIlustracaoPendenteAsync(artista);
        await CriarSessaoModeracaoAsync(aplicacao, admin, PapelUsuario.Admin);
        using var aprovada = await DecidirAsync(admin, obra, "APROVAR");
        Assert.Equal(HttpStatusCode.NoContent, aprovada.StatusCode);

        const string rota = "/api/privacidade/exclusao";
        using var semCsrf = await artista.PostAsJsonAsync(rota, new { senha = Senha, ilustracaoId = obra });
        using var senhaInvalida = await EnviarComCsrfAsync(artista, HttpMethod.Post, rota,
            new { senha = "incorreta", ilustracaoId = obra });
        using var outroTitular = await EnviarComCsrfAsync(outro, HttpMethod.Post, rota,
            new { senha = Senha, ilustracaoId = obra });
        Assert.Equal(HttpStatusCode.BadRequest, semCsrf.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, senhaInvalida.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outroTitular.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await outro.GetAsync($"/api/ilustracoes/{obra}")).StatusCode);

        using var solicitacao = await EnviarComCsrfAsync(artista, HttpMethod.Post, rota,
            new { senha = Senha, ilustracaoId = obra });
        Assert.Equal(HttpStatusCode.Accepted, solicitacao.StatusCode);
        var protocolo = (await solicitacao.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await outro.GetAsync($"/api/ilustracoes/{obra}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await outro.GetAsync($"/api/artistas/{titular}")).StatusCode);
        var processar = $"/api/privacidade/pedidos/{protocolo}/processar";
        using var acessoNegado = await EnviarComCsrfAsync(artista, HttpMethod.Post, processar, new { });
        Assert.Equal(HttpStatusCode.Forbidden, acessoNegado.StatusCode);
        using var processado = await EnviarComCsrfAsync(admin, HttpMethod.Post, processar, new { });
        Assert.Equal(HttpStatusCode.NoContent, processado.StatusCode);
        Assert.Empty(aplicacao.ImagensArmazenadas());

        using var conta = await EnviarComCsrfAsync(artista, HttpMethod.Post, rota, new { senha = Senha });
        Assert.Equal(HttpStatusCode.Accepted, conta.StatusCode);
        protocolo = (await conta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await outro.GetAsync($"/api/artistas/{titular}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await artista.GetAsync("/api/contas/sessao")).StatusCode);
        using var exclusao = await EnviarComCsrfAsync(admin, HttpMethod.Post,
            $"/api/privacidade/pedidos/{protocolo}/processar", new { });
        Assert.Equal(HttpStatusCode.NoContent, exclusao.StatusCode);

        await using var escopo = aplicacao.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        Assert.False(await db.Usuarios.AnyAsync(x => x.Id == titular));
        Assert.False(await db.Users.AnyAsync(x => x.Id == titular));
        var comprovante = await db.PedidosExclusao.SingleAsync(x => x.Id == protocolo);
        Assert.Equal(Guid.Empty, comprovante.TitularId);
        Assert.NotNull(comprovante.ConcluidoEm);
    }
}
