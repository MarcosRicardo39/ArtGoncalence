using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ArtGoncalense.Infrastructure.Identidade;

public sealed class EnvioBrevoContas(
    HttpClient http, string? remetente, IReadOnlySet<string> destinatarios, ILogger<EnvioBrevoContas> logger)
    : IEnvioContas
{
    public bool Disponivel => remetente is not null && destinatarios.Count > 0;

    public bool PermiteDestinatario(string email) => Disponivel && destinatarios.Contains(email);

    public async Task EnviarAsync(string email, string finalidade, string codigo, CancellationToken ct)
    {
        if (!PermiteDestinatario(email))
        {
            return;
        }

        var acao = finalidade == "confirmacao" ? "confirmação de e-mail" : "redefinição de senha";
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { email = remetente, name = "ArtGoncalense" },
                to = new[] { new { email } },
                subject = $"ArtGoncalense — {acao}",
                textContent = $"Seu código de {acao} no ArtGoncalense:\n\n{codigo}\n\nSe você não fez esse pedido, ignore esta mensagem."
            })
        };
        try
        {
            using var resposta = await http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                logger.LogWarning("O transporte de e-mail recusou uma solicitação. HTTP {Status}.", (int)resposta.StatusCode);
            }
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Falha de conexão com o transporte de e-mail.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Tempo limite do transporte de e-mail excedido.");
        }
    }
}
