using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArtGoncalense.Tests;

public sealed partial class ApiTests
{
    [PostgreSqlFact]
    public async Task PublicacaoValidaSessaoConfirmacaoCsrfETipoDaImagem()
    {
        using var aplicacao = new Aplicacao();
        using var cliente = await aplicacao.CriarClienteAsync();
        using var visitante = await aplicacao.CriarClienteAsync();

        using (var semSessao = await EnviarIlustracaoAsync(visitante, enviarCsrf: false))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, semSessao.StatusCode);
        }

        var (id, email) = await CriarSessaoParaPublicarAsync(cliente);
        using (var naoConfirmado = await EnviarIlustracaoAsync(cliente))
        {
            Assert.Equal(HttpStatusCode.Forbidden, naoConfirmado.StatusCode);
        }

        await ConfirmarArtistaAsync(aplicacao, cliente, email);
        using (var semCsrf = await EnviarIlustracaoAsync(cliente, enviarCsrf: false))
        {
            Assert.Equal(HttpStatusCode.BadRequest, semCsrf.StatusCode);
        }

        using (var categoriaInvalida = await EnviarIlustracaoAsync(cliente, categoria: "OUTRA"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, categoriaInvalida.StatusCode);
        }

        using (var imagemFalsa = await EnviarIlustracaoAsync(cliente, nomeArquivo: "arquivo.png",
            tipo: "image/png", imagem: [1, 2, 3]))
        {
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, imagemFalsa.StatusCode);
        }

        using (var imagemGrande = await EnviarIlustracaoAsync(cliente, imagem: new byte[LimitesImagem.EntradaBytes + 1]))
        {
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, imagemGrande.StatusCode);
        }

        Assert.Empty(aplicacao.ImagensArmazenadas());
        await using var scope = aplicacao.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        Assert.False(await db.Ilustracoes.AnyAsync(x => x.UsuarioId == id));

        using var enviada = await EnviarIlustracaoAsync(cliente, categoria: "FANART");
        Assert.Equal(HttpStatusCode.Created, enviada.StatusCode);
        using var resposta = JsonDocument.Parse(await enviada.Content.ReadAsStringAsync());
        var publicadaId = resposta.RootElement.GetProperty("id").GetGuid();
        Assert.Equal($"/api/ilustracoes/{publicadaId}", enviada.Headers.Location?.ToString());
        Assert.Equal("PENDENTE", resposta.RootElement.GetProperty("status").GetString());
        Assert.Equal("FANART", resposta.RootElement.GetProperty("categoria").GetString());
        Assert.False(resposta.RootElement.TryGetProperty("caminhoStorage", out _));

        var gravada = await db.Ilustracoes.SingleAsync(x => x.Id == publicadaId);
        Assert.Equal(id, gravada.UsuarioId);
        Assert.Equal(StatusIlustracao.Pendente, gravada.Status);
        Assert.Equal(CategoriaIlustracao.Fanart, gravada.Categoria);
        Assert.Equal(ImagemOtimizada.Tipo, gravada.MimeType);
        Assert.InRange(gravada.TamanhoBytes, 1, LimitesImagem.SaidaBytes);
        Assert.Equal(gravada.CaminhoStorage, Assert.Single(aplicacao.ImagensArmazenadas()));
    }

    [PostgreSqlFact]
    public async Task DoisEnviosConcorrentesDisputamUmaUnicaVagaSemDeixarArquivoOrfao()
    {
        using var aplicacao = new Aplicacao(sincronizarUploads: true);
        using var cliente = await aplicacao.CriarClienteAsync();
        var (id, email) = await CriarSessaoParaPublicarAsync(cliente);
        await ConfirmarArtistaAsync(aplicacao, cliente, email);

        await using var scope = aplicacao.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        var estados = new[]
        {
            StatusIlustracao.Pendente, StatusIlustracao.Pendente,
            StatusIlustracao.Aprovada, StatusIlustracao.Aprovada,
            StatusIlustracao.Rejeitada, StatusIlustracao.Oculta
        };
        var existentes = estados.Select(_ => new Ilustracao(Guid.NewGuid(), id, "Exemplo", "",
            CategoriaIlustracao.Original, "teste.webp", "teste.webp", ImagemOtimizada.Tipo, 100, DateTimeOffset.UtcNow)).ToArray();
        db.AddRange(existentes);
        await db.SaveChangesAsync();

        for (var indice = 2; indice < existentes.Length; indice++)
        {
            var exemploId = existentes[indice].Id;
            var estado = estados[indice];
            await db.Ilustracoes.Where(x => x.Id == exemploId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, estado));
        }

        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var envios = await Task.WhenAll(
            EnviarIlustracaoAsync(cliente, cancellationToken: prazo.Token),
            EnviarIlustracaoAsync(cliente, cancellationToken: prazo.Token));
        using var primeiro = envios[0];
        using var segundo = envios[1];
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
            envios.Select(resposta => resposta.StatusCode).Order().ToArray());

        var ativas = await db.Ilustracoes.CountAsync(x => x.UsuarioId == id
            && (x.Status == StatusIlustracao.Pendente || x.Status == StatusIlustracao.Aprovada));
        Assert.Equal(5, ativas);
        Assert.Equal(7, await db.Ilustracoes.CountAsync(x => x.UsuarioId == id));
        var arquivoNovo = Assert.Single(aplicacao.ImagensArmazenadas());
        Assert.True(await db.Ilustracoes.AnyAsync(x => x.UsuarioId == id && x.CaminhoStorage == arquivoNovo));

        using var excedente = await EnviarIlustracaoAsync(cliente);
        Assert.Equal(HttpStatusCode.Conflict, excedente.StatusCode);
        Assert.Single(aplicacao.ImagensArmazenadas());
    }

    private static async Task<(Guid Id, string Email)> CriarSessaoParaPublicarAsync(HttpClient cliente)
    {
        await ObterCsrfAsync(cliente);
        var email = $"{Guid.NewGuid():N}@example.test";
        using var cadastro = await PostAsync(cliente, "registrar", new { email, senha = Senha, nomePublico = "Artista" });
        Assert.Equal(HttpStatusCode.Accepted, cadastro.StatusCode);
        using var login = await PostAsync(cliente, "login", new { email, senha = Senha });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await ObterCsrfAsync(cliente);
        using var sessao = await cliente.GetFromJsonAsync<JsonDocument>("/api/contas/sessao");
        return (sessao!.RootElement.GetProperty("id").GetGuid(), email);
    }

    private static async Task ConfirmarArtistaAsync(Aplicacao aplicacao, HttpClient cliente, string email)
    {
        var codigo = Assert.Single(aplicacao.Mensagens("confirmacao"));
        using var confirmacao = await PostAsync(cliente, "confirmar-email", new { email, codigo });
        Assert.Equal(HttpStatusCode.NoContent, confirmacao.StatusCode);
    }

    private static async Task<HttpResponseMessage> EnviarIlustracaoAsync(
        HttpClient cliente, string categoria = "ORIGINAL", string nomeArquivo = "amostra.jpg",
        string tipo = "image/jpeg", byte[]? imagem = null, bool enviarCsrf = true,
        CancellationToken cancellationToken = default)
    {
        imagem ??= await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "jpeg-minimo.jpg"),
            cancellationToken);
        using var formulario = new MultipartFormDataContent();
        formulario.Add(new StringContent("Ilustração de teste"), "titulo");
        formulario.Add(new StringContent("Descrição de teste"), "descricao");
        formulario.Add(new StringContent(categoria), "categoria");
        var arquivo = new ByteArrayContent(imagem);
        arquivo.Headers.ContentType = MediaTypeHeaderValue.Parse(tipo);
        formulario.Add(arquivo, "imagem", nomeArquivo);

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/ilustracoes") { Content = formulario };
        if (enviarCsrf)
        {
            Assert.True(TokensCsrf.TryGetValue(cliente, out var csrf));
            requisicao.Headers.Add("X-XSRF-TOKEN", csrf);
        }

        return await cliente.SendAsync(requisicao, cancellationToken);
    }

    private sealed class ArmazenamentoSincronizado(IArmazenamentoImagens armazenamento) : IArmazenamentoImagens
    {
        private readonly TaskCompletionSource ambosGravados = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int gravacoes;

        public async Task<string> SalvarAsync(ImagemOtimizada imagem, CancellationToken cancellationToken)
        {
            var chave = await armazenamento.SalvarAsync(imagem, cancellationToken);
            if (Interlocked.Increment(ref gravacoes) == 2)
            {
                ambosGravados.TrySetResult();
            }

            await ambosGravados.Task.WaitAsync(cancellationToken);
            return chave;
        }

        public Task<Stream?> AbrirAsync(string chave, CancellationToken cancellationToken) =>
            armazenamento.AbrirAsync(chave, cancellationToken);

        public Task ExcluirAsync(string chave, CancellationToken cancellationToken) =>
            armazenamento.ExcluirAsync(chave, cancellationToken);
    }
}
