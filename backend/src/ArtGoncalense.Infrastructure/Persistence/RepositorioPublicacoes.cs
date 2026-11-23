using System.Data;
using ArtGoncalense.Application.Ilustracoes;
using ArtGoncalense.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtGoncalense.Infrastructure.Persistence;

public sealed class RepositorioPublicacoes(ArtGoncalenseDbContext db) : IRepositorioPublicacoes
{
    public async Task<DisponibilidadePublicacao> VerificarDisponibilidadeAsync(
        Guid artistaId, CancellationToken cancellationToken)
    {
        var artistaApto = await db.Usuarios.AnyAsync(
            artista => artista.Id == artistaId && artista.Papel == PapelUsuario.Artista && artista.Status == StatusUsuario.Ativo,
            cancellationToken);
        if (!artistaApto)
        {
            return DisponibilidadePublicacao.ArtistaIndisponivel;
        }

        var ativas = await db.Ilustracoes.CountAsync(
            ilustracao => ilustracao.UsuarioId == artistaId
                && (ilustracao.Status == StatusIlustracao.Pendente || ilustracao.Status == StatusIlustracao.Aprovada),
            cancellationToken);

        return ativas >= ServicoPublicacao.MaximoAtivasPorArtista
            ? DisponibilidadePublicacao.LimiteAtingido
            : DisponibilidadePublicacao.Disponivel;
    }

    public async Task<DisponibilidadePublicacao> TentarInserirAsync(
        Ilustracao ilustracao, CancellationToken cancellationToken)
    {
        var commitIniciado = false;
        try
        {
            await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            // O bloqueio da linha do artista serializa a contagem e a inserção entre requisições e instâncias.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT 1 FROM "Usuarios" WHERE "Id" = {ilustracao.UsuarioId} FOR NO KEY UPDATE
                """, cancellationToken);

            var disponibilidade = await VerificarDisponibilidadeAsync(ilustracao.UsuarioId, cancellationToken);
            if (disponibilidade != DisponibilidadePublicacao.Disponivel)
            {
                return disponibilidade;
            }

            db.Ilustracoes.Add(ilustracao);
            await db.SaveChangesAsync(cancellationToken);
            commitIniciado = true;
            await transacao.CommitAsync(cancellationToken);
            return DisponibilidadePublicacao.Disponivel;
        }
        catch (Exception exception) when (commitIniciado)
        {
            throw new CommitPublicacaoIndeterminadoException(exception);
        }
    }
}
