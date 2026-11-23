using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ArtGoncalense.Application.Contas;
using ArtGoncalense.Application.Denuncias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtGoncalense.API.Moderacao;

[ApiController]
[Route("api/moderacao/denuncias")]
[Authorize(Policy = AcessoConta.ModerarIlustracao)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ModeracaoDenunciasController(ServicoDenuncias denuncias) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListarRecebidas([FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var fila = await denuncias.ListarRecebidasAsync(page, pageSize, cancellationToken);
        return Ok(new { page, pageSize, total = fila.Total, itens = fila.Itens });
    }

    [HttpPost("{id:guid}/encerrar")]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Encerrar(Guid id, DadosEncerramento dados, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var moderadorId))
        {
            return Forbid();
        }

        return (await denuncias.EncerrarAsync(id, moderadorId, dados.Justificativa, cancellationToken)) switch
        {
            ResultadoEncerramento.NaoEncontrada => NotFound(),
            ResultadoEncerramento.JaEncerrada => Problem(statusCode: 409, title: "A denúncia já foi encerrada."),
            _ => NoContent()
        };
    }
}

public sealed record DadosEncerramento([Required, MaxLength(2000)] string Justificativa);
