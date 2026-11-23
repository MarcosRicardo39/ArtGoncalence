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
public sealed class IlustracoesController(ServicoPublicacao publicacao) : ControllerBase
{
    public const string PoliticaConcorrenciaUpload = "ConcorrenciaUploadIlustracao";
    private const int LimiteRequisicao = LimitesImagem.EntradaBytes + 64 * 1024;

    [Authorize(Policy = AcessoConta.PublicarIlustracao)]
    [EnableRateLimiting(PoliticaConcorrenciaUpload)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteRequisicao)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteRequisicao, ValueLengthLimit = 8192, ValueCountLimit = 8)]
    public async Task<IActionResult> Publicar([FromForm] DadosPublicacao dados, CancellationToken cancellationToken)
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
