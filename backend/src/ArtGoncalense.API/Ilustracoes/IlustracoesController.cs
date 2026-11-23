using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ArtGoncalense.Application.Contas;
using ArtGoncalense.Application.Ilustracoes;
using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ArtGoncalense.API.Ilustracoes;

[ApiController]
[Route("api/ilustracoes")]
public sealed class IlustracoesController : ControllerBase
{
    public const string PoliticaConcorrenciaUpload = "ConcorrenciaUploadIlustracao";
    private const int LimiteRequisicao = LimitesImagem.EntradaBytes + 64 * 1024;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar([FromServices] IRepositorioGaleria galeria,
        [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var resultado = await galeria.ListarAsync(page, pageSize, cancellationToken);
        return Ok(new { page, pageSize, total = resultado.Total, itens = resultado.Itens.Select(RespostaPublica) });
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Buscar(Guid id, [FromServices] IRepositorioGaleria galeria,
        CancellationToken cancellationToken)
    {
        var item = await galeria.BuscarAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(RespostaPublica(item));
    }

    [HttpGet("{id:guid}/imagem")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Imagem(Guid id, [FromServices] IRepositorioGaleria galeria,
        [FromServices] IArmazenamentoImagens armazenamento, CancellationToken cancellationToken)
    {
        var chave = await galeria.BuscarChaveAsync(id, cancellationToken);
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

    private static object RespostaPublica(IlustracaoPublica item) => new
    {
        id = item.Id,
        titulo = item.Titulo,
        descricao = item.Descricao,
        categoria = item.Categoria == CategoriaIlustracao.Original ? "ORIGINAL" : "FANART",
        criadoEm = item.CriadoEm,
        artista = new { id = item.ArtistaId, nomePublico = item.NomePublico },
        imagemUrl = $"/api/ilustracoes/{item.Id}/imagem"
    };

    [Authorize(Policy = AcessoConta.PublicarIlustracao)]
    [EnableRateLimiting(PoliticaConcorrenciaUpload)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteRequisicao)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteRequisicao, ValueLengthLimit = 8192, ValueCountLimit = 8)]
    public async Task<IActionResult> Publicar([FromForm] DadosPublicacao dados,
        [FromServices] ServicoPublicacao publicacao, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var artistaId))
        {
            return Forbid();
        }

        if (Request.Form.Files.Count != 1 || dados.Imagem.Length == 0)
        {
            return Problem(statusCode: 400, title: "Envie uma imagem.");
        }

        if (dados.Imagem.Length > LimitesImagem.EntradaBytes)
        {
            return Problem(statusCode: 413, title: "A imagem enviada deve ter no máximo 10 MB.");
        }

        var categoria = dados.Categoria == "ORIGINAL" ? CategoriaIlustracao.Original : CategoriaIlustracao.Fanart;
        try
        {
            await using var fluxo = dados.Imagem.OpenReadStream();
            var resultado = await publicacao.PublicarAsync(artistaId, dados.Titulo.Trim(), dados.Descricao?.Trim() ?? "",
                categoria, fluxo, dados.Imagem.FileName, dados.Imagem.ContentType, cancellationToken);

            return resultado.Situacao switch
            {
                DisponibilidadePublicacao.LimiteAtingido => Problem(statusCode: 409,
                    title: "Você atingiu o limite de cinco publicações ativas."),
                DisponibilidadePublicacao.ArtistaIndisponivel => Forbid(),
                _ => Created($"/api/ilustracoes/{resultado.Id}", new
                {
                    id = resultado.Id,
                    titulo = dados.Titulo.Trim(),
                    categoria = dados.Categoria,
                    status = "PENDENTE"
                })
            };
        }
        catch (ImagemInvalidaException exception)
        {
            var status = exception.Motivo switch
            {
                MotivoImagemInvalida.TamanhoEntrada or MotivoImagemInvalida.TamanhoSaida => 413,
                MotivoImagemInvalida.Formato => 415,
                _ => 400
            };

            return Problem(statusCode: status, title: exception.Message);
        }
    }
}

public sealed class DadosPublicacao
{
    [Required, MaxLength(200)]
    public string Titulo { get; init; } = "";

    [MaxLength(2000)]
    public string? Descricao { get; init; }

    [Required, RegularExpression(@"\A(?:ORIGINAL|FANART)\z")]
    public string Categoria { get; init; } = "";

    [Required]
    public IFormFile Imagem { get; init; } = null!;
}
