using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json.Serialization;
using ArtGoncalense.API.Contas;
using ArtGoncalense.Application.Contas;
using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ArtGoncalense.API;

[ApiController]
[Route("api/privacidade")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PrivacidadeController(Privacidade privacidade) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Contato([FromServices] IConfiguration configuracao)
    {
        var email = configuracao["Privacidade:EmailContato"];
        return Ok(new { email = MailAddress.TryCreate(email, out var endereco) && endereco.Address == email ? email : null });
    }

    [Authorize(Roles = AcessoConta.PapelArtista)]
    [HttpPost("exclusao")]
    [EnableRateLimiting(ConfiguracaoContas.Exclusao)]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Solicitar(PedidoPrivacidade dados, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var titular))
        {
            return Forbid();
        }

        var (status, id) = await privacidade.SolicitarAsync(titular, dados.IlustracaoId, dados.Senha, ct);
        return status switch
        {
            202 => Accepted(new { id }),
            403 => Problem(statusCode: 403, title: "Não foi possível confirmar a senha."),
            _ => NotFound()
        };
    }

    [Authorize(Roles = nameof(PapelUsuario.Admin))]
    [HttpGet("pedidos")]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var pedidos = await privacidade.ListarAsync(ct);
        return Ok(pedidos.Select(p => new { p.Id, p.TitularId, p.IlustracaoId, p.SolicitadoEm, limiteEm = p.SolicitadoEm.AddDays(30) }));
    }

    [Authorize(Roles = nameof(PapelUsuario.Admin))]
    [HttpPost("pedidos/{id:guid}/processar")]
    public async Task<IActionResult> Processar(Guid id, [FromServices] IArmazenamentoImagens armazenamento,
        CancellationToken ct) =>
        (await privacidade.ProcessarAsync(id, armazenamento, ct)) switch
        {
            ResultadoExclusao.Concluida => NoContent(),
            ResultadoExclusao.Conservacao => Problem(statusCode: 409, title: "O pedido tem registros sujeitos a conservação."),
            _ => NotFound()
        };

    [Authorize(Roles = nameof(PapelUsuario.Admin))]
    [HttpPost("contatos/limpar")]
    public async Task<IActionResult> LimparContatos(CancellationToken ct) =>
        Ok(new { removidos = await privacidade.LimparContatosAsync(ct) });

    [Authorize(Roles = nameof(PapelUsuario.Admin))]
    [HttpPut("denuncias/{id:guid}/conservacao")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> DefinirConservacao(Guid id, ConservacaoContato dados, CancellationToken ct)
    {
        if (dados.Motivo is not null && string.IsNullOrWhiteSpace(dados.Motivo))
        {
            return Problem(statusCode: 400, title: "Informe a justificativa de conservação.");
        }

        return await privacidade.RegistrarConservacaoAsync(id, dados.Motivo?.Trim(), ct) == 0
            ? NotFound()
            : NoContent();
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PedidoPrivacidade([Required, MaxLength(AcessoConta.SenhaMaxima)] string Senha, Guid? IlustracaoId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConservacaoContato([property: JsonRequired] [MaxLength(500)] string? Motivo);
