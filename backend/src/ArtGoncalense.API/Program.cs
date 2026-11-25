using ArtGoncalense.API.Contas;
using ArtGoncalense.API;
using ArtGoncalense.API.Ilustracoes;
using ArtGoncalense.Application.Artistas;
using ArtGoncalense.Application.Denuncias;
using ArtGoncalense.Application.Ilustracoes;
using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Application.Moderacao;
using ArtGoncalense.Infrastructure.Imagens;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Piloto:Ativo"))
{
    builder.Services.AdicionarProtecaoPiloto(builder.Configuration["Piloto:CertificadoProtecao"]);
}
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var endereco in builder.Configuration.GetSection("Piloto:ProxiesConfiaveis").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(System.Net.IPAddress.Parse(endereco));
    }
});

// Add services to the container.

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddProblemDetails();
builder.AdicionarContas();
builder.Services.AddScoped<ServicoPerfis>();
builder.Services.AddScoped<Privacidade>();
builder.Services.AddScoped<IRepositorioPerfis, RepositorioPerfis>();
builder.Services.AddScoped<ServicoPublicacao>();
builder.Services.AddScoped<IRepositorioPublicacoes, RepositorioPublicacoes>();
builder.Services.AddScoped<IRepositorioGaleria, RepositorioGaleria>();
builder.Services.AddScoped<ServicoModeracao>();
builder.Services.AddScoped<IRepositorioModeracao, RepositorioModeracao>();
builder.Services.AddScoped<ServicoDenuncias>();
builder.Services.AddScoped<IRepositorioDenuncias, RepositorioDenuncias>();
builder.Services.AddRateLimiter(options => options.AddConcurrencyLimiter(
    IlustracoesController.PoliticaConcorrenciaUpload, limiter =>
    {
        limiter.PermitLimit = 4;
        limiter.QueueLimit = 0;
    }));
builder.Services.AddSingleton<IProcessadorImagens, ProcessadorImagens>();
builder.Services.AddSingleton<ArmazenamentoImagensLocal>(services =>
{
    var ambiente = services.GetRequiredService<IWebHostEnvironment>();
    var diretorio = services.GetRequiredService<IConfiguration>()["Imagens:DiretorioLocal"];
    if (!ambiente.IsDevelopment() || string.IsNullOrWhiteSpace(diretorio))
    {
        throw new InvalidOperationException("Configure o armazenamento privado de imagens de desenvolvimento.");
    }

    var raiz = Path.GetFullPath(diretorio);
    var relativo = Path.GetRelativePath(ambiente.ContentRootPath, raiz);
    if (!Path.IsPathFullyQualified(diretorio) || relativo == "."
        || (!Path.IsPathRooted(relativo) && relativo != ".."
            && !relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
    {
        throw new InvalidOperationException("Use um diretório absoluto fora da raiz da aplicação para as imagens.");
    }

    return new ArmazenamentoImagensLocal(raiz);
});
builder.Services.AddHttpClient<ArmazenamentoSupabase>((services, client) =>
{
    var config = services.GetRequiredService<IConfiguration>();
    var chave = config["Imagens:Supabase:Chave"];
    if (!Uri.TryCreate(config["Imagens:Supabase:Url"], UriKind.Absolute, out var url)
        || url.Scheme != Uri.UriSchemeHttps || !url.IsDefaultPort || url.PathAndQuery != "/"
        || url.Fragment.Length != 0 || url.UserInfo.Length != 0
        || !url.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(chave) || !chave.StartsWith("sb_secret_", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Configure a URL e a chave secreta de Storage do Supabase.");
    }

    client.BaseAddress = url;
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("apikey", chave);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<IArmazenamentoImagens>(services =>
    services.GetRequiredService<IWebHostEnvironment>().IsDevelopment()
        ? services.GetRequiredService<ArmazenamentoImagensLocal>()
        : services.GetRequiredService<ArmazenamentoSupabase>());
builder.Services.AddScoped<ReconciliadorImagens>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<SaudePostgreSql>("postgresql", timeout: TimeSpan.FromSeconds(5));
builder.Services.AddDbContext<ArtGoncalenseDbContext>((services, options) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("ArtGoncalense");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Configure ConnectionStrings__ArtGoncalense.");
    }

    options.UseNpgsql(connectionString);
});

var app = builder.Build();
var piloto = app.Configuration.GetValue<bool>("Piloto:Ativo");
if (piloto)
{
    app.UseForwardedHeaders();
    app.UseMiddleware<ArtGoncalense.API.AcessoPiloto>();
    app.UseStaticFiles();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
if (piloto)
{
    app.MapFallback(async contexto =>
    {
        if (contexto.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            contexto.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        contexto.Response.ContentType = "text/html; charset=utf-8";
        await contexto.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
    });
}

app.Run();

public partial class Program;

internal sealed class SaudePostgreSql(ArtGoncalenseDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
}
