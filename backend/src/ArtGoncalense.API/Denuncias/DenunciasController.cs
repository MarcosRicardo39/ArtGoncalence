using System.ComponentModel.DataAnnotations;
using ArtGoncalense.Application.Denuncias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ArtGoncalense.API.Denuncias;

[ApiController]
[Route("api/ilustracoes/{id:guid}/denuncias")]
public sealed class DenunciasController(ServicoDenuncias denuncias) : ControllerBase
{
    public const string PoliticaPorIp = "DenunciasPorIp";

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaPorIp)]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Registrar(Guid id, DadosDenuncia dados, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        if (dados.Motivo == "DIREITOS_AUTORAIS" && string.IsNullOrWhiteSpace(dados.Contato))
        {
            return Problem(statusCode: 400, title: "Informe um e-mail de contato para notificações de direitos autorais.");
        }

        return await denuncias.RegistrarAsync(id, dados.Motivo, dados.Descricao, dados.Contato, cancellationToken)
            ? Accepted()
            : NotFound();
    }
}

public sealed record DadosDenuncia(
    [Required, RegularExpression(@"\A(?:DIREITOS_AUTORAIS|OUTRO)\z")] string Motivo,
    [Required, MaxLength(2000)] string Descricao,
    [EmailAddress, MaxLength(256)] string? Contato);
