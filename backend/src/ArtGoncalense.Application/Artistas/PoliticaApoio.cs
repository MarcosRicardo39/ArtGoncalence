using System.Text.RegularExpressions;

namespace ArtGoncalense.Application.Artistas;

public sealed record ApoioPublico(string Provedor, string Url);

internal static partial class PoliticaApoio
{
    private static readonly HashSet<string> RotasReservadas = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "account", "api", "checkout", "contact", "dashboard", "discover", "explore", "go",
        "help", "home", "login", "logout", "manage", "payment", "payments", "privacy", "redirect",
        "register", "settings", "shop", "signup", "support", "terms"
    };

    public static ApoioPublico? Validar(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var correspondencia = PerfilKoFi().Match(url.Trim());
        if (!correspondencia.Success || RotasReservadas.Contains(correspondencia.Groups[1].Value))
        {
            return null;
        }

        return new ApoioPublico("Ko-fi", $"https://ko-fi.com/{correspondencia.Groups[1].Value}");
    }

    [GeneratedRegex(@"\Ahttps://ko-fi\.com/([A-Za-z0-9][A-Za-z0-9_]{0,63})/?\z", RegexOptions.CultureInvariant)]
    private static partial Regex PerfilKoFi();
}
