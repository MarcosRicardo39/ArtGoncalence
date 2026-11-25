using System.Security.Cryptography;
using System.Text;

namespace ArtGoncalense.API;

internal sealed class AcessoPiloto(RequestDelegate proximo, IConfiguration configuracao)
{
    private readonly byte[] esperado = Credencial(configuracao["Piloto:Senha"]);

    public async Task InvokeAsync(HttpContext contexto)
    {
        if (contexto.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await proximo(contexto);
            return;
        }

        contexto.Response.Headers.CacheControl = "private, no-store";
        if (!contexto.Request.IsHttps)
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var cabecalho = contexto.Request.Headers.Authorization.ToString();
        var recebido = SHA256.HashData(Encoding.UTF8.GetBytes(cabecalho));
        if (!CryptographicOperations.FixedTimeEquals(esperado, recebido))
        {
            contexto.Response.Headers.WWWAuthenticate = "Basic realm=\"ArtGoncalense piloto\"";
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        contexto.Request.Headers.Remove("Authorization");
        await proximo(contexto);
    }

    private static byte[] Credencial(string? senha)
    {
        if (senha is null || senha.Length is < 32 or > 128
            || senha.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
        {
            throw new InvalidOperationException("Configure Piloto__Senha com um segredo aleatório de 32 a 128 caracteres ASCII seguros.");
        }

        var valor = Convert.ToBase64String(Encoding.ASCII.GetBytes($"piloto:{senha}"));
        return SHA256.HashData(Encoding.ASCII.GetBytes($"Basic {valor}"));
    }
}
