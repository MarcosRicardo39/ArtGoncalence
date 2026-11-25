using System.Net;
using System.Text.Json;
using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Infrastructure.Identidade;
using ArtGoncalense.Infrastructure.Imagens;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtGoncalense.Tests;

public sealed class IntegracoesPilotoTests
{
    [Fact]
    public async Task SupabaseGravaLeEExcluiSomenteNoBucketPrivado()
    {
        var dados = "RIFF0000WEBP"u8.ToArray();
        string? chave = null;
        using var http = new HttpClient(new Transporte(async pedido =>
        {
            var rota = pedido.RequestUri!.AbsolutePath;
            if (pedido.Method == HttpMethod.Post)
            {
                Assert.StartsWith("/storage/v1/object/ilustracoes/", rota);
                chave = rota.Split('/').Last();
                Assert.True(Guid.TryParseExact(chave[..^5], "N", out _));
                Assert.EndsWith(".webp", chave);
                Assert.Equal("false", Assert.Single(pedido.Headers.GetValues("x-upsert")));
                Assert.Equal("image/webp", pedido.Content!.Headers.ContentType?.MediaType);
                Assert.Equal(dados, await pedido.Content.ReadAsByteArrayAsync());
            }
            else if (pedido.Method == HttpMethod.Get)
            {
                Assert.Equal($"/storage/v1/object/authenticated/ilustracoes/{chave}", rota);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(dados) };
            }
            else
            {
                Assert.Equal(HttpMethod.Delete, pedido.Method);
                Assert.Equal("/storage/v1/object/ilustracoes", rota);
                using var json = JsonDocument.Parse(await pedido.Content!.ReadAsStringAsync());
                Assert.Equal(chave, json.RootElement.GetProperty("prefixes")[0].GetString());
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        })) { BaseAddress = new Uri("https://storage.example.test/") };
        var armazenamento = new ArmazenamentoSupabase(http);
        var salva = await armazenamento.SalvarAsync(new ImagemOtimizada(dados, 1, 1), default);
        Assert.Equal(chave, salva);
        await using var imagem = await armazenamento.AbrirAsync(salva, default);
        using var recebida = new MemoryStream();
        await imagem!.CopyToAsync(recebida);
        Assert.Equal(dados, recebida.ToArray());
        await armazenamento.ExcluirAsync(salva, default);
        await Assert.ThrowsAsync<ArgumentException>(() => armazenamento.ExcluirAsync("../outro.webp", default));
    }

    [Fact]
    public async Task BrevoSoEnviaAosDestinatariosPermitidos()
    {
        var chamadas = 0;
        using var http = new HttpClient(new Transporte(async pedido =>
        {
            chamadas++;
            Assert.Equal(HttpMethod.Post, pedido.Method);
            Assert.Equal("/v3/smtp/email", pedido.RequestUri!.AbsolutePath);
            using var json = JsonDocument.Parse(await pedido.Content!.ReadAsStringAsync());
            Assert.Equal("artista@example.test", json.RootElement.GetProperty("to")[0].GetProperty("email").GetString());
            Assert.Contains("codigo-ficticio", json.RootElement.GetProperty("textContent").GetString());
            return new HttpResponseMessage(HttpStatusCode.Created);
        })) { BaseAddress = new Uri("https://mail.example.test/") };
        var envio = new EnvioBrevoContas(http, "remetente@example.test",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "artista@example.test" },
            NullLogger<EnvioBrevoContas>.Instance);

        await envio.EnviarAsync("fora@example.test", "confirmacao", "codigo-ficticio", default);
        Assert.Equal(0, chamadas);
        await envio.EnviarAsync("artista@example.test", "confirmacao", "codigo-ficticio", default);
        Assert.Equal(1, chamadas);
    }

    private sealed class Transporte(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => responder(request);
    }
}
