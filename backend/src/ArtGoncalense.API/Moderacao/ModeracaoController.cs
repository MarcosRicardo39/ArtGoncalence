using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ArtGoncalense.Application.Contas;
using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Application.Moderacao;
using ArtGoncalense.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtGoncalense.API.Moderacao;

[ApiController]
[Route("api/moderacao")]
[Authorize(Policy = AcessoConta.ModerarIlustracao)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ModeracaoController(ServicoModeracao moderacao) : ControllerBase
{
    [HttpGet("pendentes")]
    public async Task<IActionResult> ListarPendentes([FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var fila = await moderacao.ListarPendentesAsync(page, pageSize, cancellationToken);
        return Ok(new
        {
            page,
            pageSize,
            total = fila.Total,
            itens = fila.Itens.Select(item => new
            {
                id = item.Id,
                artistaId = item.ArtistaId,
                titulo = item.Titulo,
                descricao = item.Descricao,
                categoria = item.Categoria == CategoriaIlustracao.Original ? "ORIGINAL" : "FANART",
                status = "PENDENTE",
                criadoEm = item.CriadoEm,
                imagemUrl = $"/api/moderacao/{item.Id}/imagem"
            })
        });
    }

    [HttpGet("{id:guid}/imagem")]
    public async Task<IActionResult> ObterImagem(Guid id, [FromServices] IArmazenamentoImagens armazenamento,
        CancellationToken cancellationToken)
    {
        var chave = await moderacao.BuscarChaveImagemAsync(id, cancellationToken);
        if (chave is null)
        {
            return NotFound();
        }

        Stream? fluxo;
        try
        {
            fluxo = await armazenamento.AbrirAsync(chave, cancellationToken);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }

        if (fluxo is null)
        {
            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        return File(fluxo, ImagemOtimizada.Tipo);
    }

    [HttpPost("{id:guid}/decisao")]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Decidir(Guid id, DadosDecisao dados, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var moderadorId))
        {
            return Forbid();
        }

        if (id == Guid.Empty)
        {
            return NotFound();
        }

        var acao = dados.Acao switch
        {
            "APROVAR" => TipoAcaoModeracao.Aprovar,
            "REJEITAR" => TipoAcaoModeracao.Rejeitar,
            _ => TipoAcaoModeracao.Ocultar
        };

        return (await moderacao.DecidirAsync(id, moderadorId, acao, dados.Justificativa, cancellationToken)) switch
        {
            ResultadoModeracao.NaoEncontrada => NotFound(),
            ResultadoModeracao.EstadoIncompativel => Problem(statusCode: 409,
                title: "A decisão não é permitida para o estado atual da ilustração."),
            _ => NoContent()
        };
    }
}

public sealed record DadosDecisao(
    [Required, RegularExpression(@"\A(?:APROVAR|REJEITAR|OCULTAR)\z")] string Acao,
    [Required, MaxLength(2000)] string Justificativa);
