using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;
using ArtGoncalense.Application.Artistas;
using ArtGoncalense.Application.Contas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtGoncalense.API.Artistas;

[ApiController]
[Route("api/artistas")]
public sealed class ArtistasController(ServicoPerfis perfis) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PerfilPublico>> Buscar(Guid id, CancellationToken cancellationToken)
    {
        var perfil = await perfis.BuscarPublicoAsync(id, cancellationToken);
        return perfil is null ? NotFound() : Ok(perfil);
    }

    [Authorize(Roles = AcessoConta.PapelArtista)]
    [HttpPut("me")]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Editar(EdicaoPerfil dados, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var titularId))
        {
            return Forbid();
        }

        return await perfis.EditarAsync(titularId, dados.NomePublico, dados.Bio, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [Authorize(Roles = AcessoConta.PapelArtista)]
    [HttpPut("me/apoio")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> DefinirApoio(EdicaoApoio dados, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var titularId))
        {
            return Forbid();
        }

        return (await perfis.DefinirApoioAsync(titularId, dados.Url, cancellationToken)) switch
        {
            ResultadoApoio.UrlInvalida => Problem(statusCode: 400,
                title: "Use um link direto como https://ko-fi.com/seuperfil."),
            ResultadoApoio.ArtistaIndisponivel => NotFound(),
            _ => NoContent()
        };
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EdicaoPerfil(
    [Required, MaxLength(200)] string NomePublico,
    [MaxLength(2000)] string? Bio);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EdicaoApoio([property: JsonRequired] [MaxLength(256)] string? Url);
