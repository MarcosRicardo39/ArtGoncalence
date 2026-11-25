using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArtGoncalense.Application.Imagens;

namespace ArtGoncalense.Infrastructure.Imagens;

public sealed class ArmazenamentoSupabase(HttpClient http) : IArmazenamentoImagens
{
    private const string Bucket = "ilustracoes";

    public async Task<string> SalvarAsync(ImagemOtimizada imagem, CancellationToken ct)
    {
        if (!ArmazenamentoImagensLocal.WebpValido(imagem.Bytes))
        {
            throw new ArgumentException("O armazenamento aceita apenas WebP otimizado.", nameof(imagem));
        }

        var chave = $"{Guid.NewGuid():N}.webp";
        using var pedido = new HttpRequestMessage(HttpMethod.Post, $"storage/v1/object/{Bucket}/{chave}")
        {
            Content = new ByteArrayContent(imagem.Bytes)
        };
        pedido.Content.Headers.ContentType = new MediaTypeHeaderValue(ImagemOtimizada.Tipo);
        pedido.Headers.Add("x-upsert", "false");
        using var resposta = await http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, ct);
        resposta.EnsureSuccessStatusCode();
        return chave;
    }

    public async Task<Stream?> AbrirAsync(string chave, CancellationToken ct)
    {
        ValidarChave(chave);
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        prazo.CancelAfter(TimeSpan.FromSeconds(15));
        using var resposta = await http.GetAsync($"storage/v1/object/authenticated/{Bucket}/{chave}",
            HttpCompletionOption.ResponseHeadersRead, prazo.Token);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        resposta.EnsureSuccessStatusCode();
        await using var origem = await resposta.Content.ReadAsStreamAsync(prazo.Token);
        var dados = new byte[LimitesImagem.SaidaBytes + 1];
        var tamanho = await origem.ReadAtLeastAsync(dados, dados.Length, false, prazo.Token);
        if (!ArmazenamentoImagensLocal.WebpValido(dados.AsSpan(0, tamanho)))
        {
            throw new IOException("O armazenamento retornou uma imagem inválida.");
        }

        return new MemoryStream(dados, 0, tamanho, writable: false);
    }

    public async Task ExcluirAsync(string chave, CancellationToken ct)
    {
        ValidarChave(chave);
        using var pedido = new HttpRequestMessage(HttpMethod.Delete, $"storage/v1/object/{Bucket}")
        {
            Content = JsonContent.Create(new { prefixes = new[] { chave } })
        };
        using var resposta = await http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, ct);
        resposta.EnsureSuccessStatusCode();
    }

    private static void ValidarChave(string chave)
    {
        if (!ArmazenamentoImagensLocal.ChaveValida(chave))
        {
            throw new ArgumentException("Chave de imagem inválida.", nameof(chave));
        }
    }
}
