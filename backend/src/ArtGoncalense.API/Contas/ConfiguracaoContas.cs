using System.Threading.RateLimiting;
using ArtGoncalense.API.Denuncias;
using ArtGoncalense.Application.Contas;
using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Identidade;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArtGoncalense.API.Contas;

internal static class ConfiguracaoContas
{
    internal const string Login = "Login";
    internal const string Correio = "Correio";
    internal const string Codigo = "Codigo";

    public static void AdicionarContas(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;
        var loginPorIp = Limite(config, "LoginPorIp", 20);
        var correioPorIp = Limite(config, "CorreioPorIp", 5);
        var enviosPorEmail = Limite(config, "EnviosPorEmail", 3);
        var falhasPorConta = Limite(config, "FalhasPorConta", 5);
        var janela = TimeSpan.FromMinutes(15);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new LimiteEnvioConta(enviosPorEmail, sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new EnvioLocalContas(
            sp.GetRequiredService<IWebHostEnvironment>().IsDevelopment()
                ? sp.GetRequiredService<IConfiguration>()["Contas:DiretorioEmailLocal"]
                : null,
            sp.GetRequiredService<ILogger<EnvioLocalContas>>()));
        services.AddScoped<IServicoContas, ServicoContas>();
        services.AddIdentityCore<ContaIdentity>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = AcessoConta.SenhaMinima;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredUniqueChars = 1;
            options.Lockout.MaxFailedAccessAttempts = falhasPorConta;
            options.Lockout.DefaultLockoutTimeSpan = janela;
        })
            .AddEntityFrameworkStores<ArtGoncalenseDbContext>()
            .AddClaimsPrincipalFactory<PrincipalContaFactory>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-ArtGoncalense";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = false;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.Events.OnValidatePrincipal = async context =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                context.ShouldRenew = false;
                if (context.Principal?.HasClaim(AcessoConta.ContaDisponivel, bool.TrueString) != true)
                {
                    context.RejectPrincipal();
                }
            };
        });
        services.AddAuthorizationBuilder()
            .AddPolicy(AcessoConta.PublicarIlustracao, policy => policy
                .RequireAuthenticatedUser().RequireRole(AcessoConta.PapelArtista)
                .RequireClaim(AcessoConta.EmailConfirmado, bool.TrueString))
            .AddPolicy(AcessoConta.ModerarIlustracao, policy => policy
                .RequireAuthenticatedUser().RequireRole(nameof(PapelUsuario.Moderador), nameof(PapelUsuario.Admin)));
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.SuppressReadingTokenFromFormBody = true;
            options.Cookie.Name = "__Host-ArtGoncalense-Csrf";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
                CriarProblema(context.HttpContext, StatusCodes.Status400BadRequest, "Dados da solicitação inválidos."));
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problema = CriarProblema(context.HttpContext, StatusCodes.Status429TooManyRequests,
                    "Muitas solicitações. Tente novamente mais tarde.");
                await context.HttpContext.Response.WriteAsJsonAsync(problema,
                    options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
            };
            options.AddPolicy(Login, context => Particao(context, loginPorIp, janela));
            options.AddPolicy(Correio, context => Particao(context, correioPorIp, janela));
            options.AddPolicy(Codigo, context => Particao(context, loginPorIp, janela));
            options.AddPolicy(DenunciasController.PoliticaPorIp, context => ParticaoPorIp(context, 5, janela));
        });
    }

    private static ProblemDetails CriarProblema(HttpContext context, int status, string titulo)
    {
        return context.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateProblemDetails(context, status, title: titulo);
    }

    private static RateLimitPartition<string> Particao(HttpContext context, int limite, TimeSpan janela)
    {
        return ParticaoPorIp(context, limite, janela, context.GetEndpoint()?.DisplayName);
    }

    private static RateLimitPartition<string> ParticaoPorIp(HttpContext context, int limite, TimeSpan janela,
        string? operacao = null)
    {
        var endereco = context.Connection.RemoteIpAddress;
        if (endereco?.IsIPv4MappedToIPv6 == true)
        {
            endereco = endereco.MapToIPv4();
        }

        var chave = $"{operacao}:{endereco}";
        return RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limite,
            Window = janela,
            QueueLimit = 0
        });
    }

    private static int Limite(IConfiguration config, string nome, int valorPadrao)
    {
        var valor = config.GetValue($"Contas:Limites:{nome}", valorPadrao);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor, nome);
        return valor;
    }
}
