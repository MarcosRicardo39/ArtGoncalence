using ArtGoncalense.API.Contas;
using ArtGoncalense.API.Ilustracoes;
using ArtGoncalense.Application.Artistas;
using ArtGoncalense.Application.Denuncias;
using ArtGoncalense.Application.Ilustracoes;
using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Application.Moderacao;
using ArtGoncalense.Infrastructure.Imagens;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddProblemDetails();
builder.AdicionarContas();
builder.Services.AddScoped<ServicoPerfis>();
builder.Services.AddScoped<IRepositorioPerfis, RepositorioPerfis>();
builder.Services.AddScoped<ServicoPublicacao>();
builder.Services.AddScoped<IRepositorioPublicacoes, RepositorioPublicacoes>();
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
builder.Services.AddSingleton<IArmazenamentoImagens>(services => services.GetRequiredService<ArmazenamentoImagensLocal>());
builder.Services.AddScoped<ReconciliadorImagens>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
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

app.Run();

public partial class Program;
