using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ArtGoncalense.Tests;

public sealed partial class ApiTests
{
    [PostgreSqlFact]
    public async Task SaudeRespondeSemSessaoEIndicaIndisponibilidadeDoBanco()
    {
        using var aplicacao = new Aplicacao();
        using var cliente = await aplicacao.CriarClienteAsync();
        using var saudavel = await cliente.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, saudavel.StatusCode);
        Assert.Equal("Healthy", await saudavel.Content.ReadAsStringAsync());

        var conexao = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(PostgreSqlFactAttribute.ConnectionVariable))
        {
            Database = $"ausente_{Guid.NewGuid():N}"
        };
        using var semBanco = aplicacao.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:ArtGoncalense"] = conexao.ConnectionString })));
        using var visitante = semBanco.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var indisponivel = await visitante.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, indisponivel.StatusCode);
        Assert.Equal("Unhealthy", await indisponivel.Content.ReadAsStringAsync());
    }
}
