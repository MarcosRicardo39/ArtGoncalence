using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ArtGoncalense.Application.Contas;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ArtGoncalense.API.Contas;

[ApiController]
[Route("api/contas")]
[RequestSizeLimit(16_384)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ContasController(IServicoContas contas) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracaoContas.Correio)]
    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar(RegistroConta dados, CancellationToken cancellationToken)
    {
        if (!dados.MaiorDeIdade)
        {
            return Problem(statusCode: 400, title: "O cadastro exige a declaração de ter 18 anos ou mais.");
        }

        if (!contas.EnvioDisponivel)
        {
            return CorreioIndisponivel();
        }

        return await contas.RegistrarAsync(dados.Email.Trim(), dados.Senha, dados.NomePublico, cancellationToken)
            ? SolicitacaoAceita()
            : Problem(statusCode: 400, title: "Dados da solicitação inválidos.");
    }

    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracaoContas.Login)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginConta dados, CancellationToken cancellationToken)
    {
        return await contas.LoginAsync(dados.Email.Trim(), dados.Senha, cancellationToken)
            ? Ok()
            : Problem(statusCode: 401, title: "E-mail ou senha inválidos.");
    }

    [Authorize]
    [HttpGet("sessao")]
    public IActionResult Sessao() => Ok(new
    {
        id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        emailConfirmado = User.HasClaim(AcessoConta.EmailConfirmado, bool.TrueString)
    });

    [Authorize]
    [HttpPost("sair")]
    public async Task<IActionResult> Sair()
    {
        await contas.SairAsync();
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracaoContas.Codigo)]
    [HttpPost("confirmar-email")]
    public async Task<IActionResult> ConfirmarEmail(CodigoConta dados, CancellationToken cancellationToken)
    {
        return await contas.ConfirmarAsync(dados.Email.Trim(), dados.Codigo, cancellationToken)
            ? NoContent()
            : CodigoInvalido();
    }

    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracaoContas.Correio)]
    [HttpPost("reenviar-confirmacao")]
    public async Task<IActionResult> ReenviarConfirmacao(EmailConta dados, CancellationToken cancellationToken)
    {
        if (!contas.EnvioDisponivel)
        {
            return CorreioIndisponivel();
        }

        await contas.ReenviarConfirmacaoAsync(dados.Email.Trim(), cancellationToken);
        return SolicitacaoAceita();
    }

    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracaoContas.Correio)]
    [HttpPost("solicitar-recuperacao")]
    public async Task<IActionResult> SolicitarRecuperacao(EmailConta dados, CancellationToken cancellationToken)
    {
        if (!contas.EnvioDisponivel)
        {
            return CorreioIndisponivel();
        }

        await contas.SolicitarRecuperacaoAsync(dados.Email.Trim(), cancellationToken);
        return SolicitacaoAceita();
    }

    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracaoContas.Codigo)]
    [HttpPost("redefinir-senha")]
    public async Task<IActionResult> RedefinirSenha(NovaSenhaConta dados, CancellationToken cancellationToken)
    {
        return await contas.RedefinirSenhaAsync(dados.Email.Trim(), dados.Codigo, dados.Senha, cancellationToken)
            ? NoContent()
            : CodigoInvalido();
    }

    private AcceptedResult SolicitacaoAceita() =>
        Accepted(value: new { mensagem = "Se aplicável, enviaremos instruções por e-mail." });

    private ObjectResult CodigoInvalido() => Problem(statusCode: 400, title: "Código inválido ou expirado.");

    private ObjectResult CorreioIndisponivel() => Problem(statusCode: 503, title: "Serviço de e-mail indisponível.");
}

public sealed record EmailConta([Required, EmailAddress, MaxLength(256)] string Email);

public sealed record LoginConta(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(AcessoConta.SenhaMaxima)] string Senha);

public sealed record RegistroConta(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, StringLength(AcessoConta.SenhaMaxima, MinimumLength = AcessoConta.SenhaMinima)] string Senha,
    [Required, MaxLength(200)] string NomePublico,
    bool MaiorDeIdade);

public sealed record CodigoConta(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(4096)] string Codigo);

public sealed record NovaSenhaConta(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(4096)] string Codigo,
    [Required, StringLength(AcessoConta.SenhaMaxima, MinimumLength = AcessoConta.SenhaMinima)] string Senha);
