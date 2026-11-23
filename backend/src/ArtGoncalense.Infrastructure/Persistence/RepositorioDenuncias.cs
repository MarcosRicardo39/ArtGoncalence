using System.Data;
using ArtGoncalense.Application.Denuncias;
using ArtGoncalense.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtGoncalense.Infrastructure.Persistence;

public sealed class RepositorioDenuncias(ArtGoncalenseDbContext db) : IRepositorioDenuncias
{
    public async Task<bool> RegistrarAsync(Denuncia denuncia, CancellationToken cancellationToken)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Impede que a ilustração deixe de estar aprovada entre a validação e a gravação da denúncia.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM "Ilustracoes" WHERE "Id" = {denuncia.IlustracaoId} FOR SHARE
            """, cancellationToken);
        var aprovada = await db.Ilustracoes.AnyAsync(
            x => x.Id == denuncia.IlustracaoId && x.Status == StatusIlustracao.Aprovada, cancellationToken);
        if (!aprovada)
        {
            return false;
        }

        db.Denuncias.Add(denuncia);
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<FilaDenuncias> ListarRecebidasAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var consulta = db.Denuncias.AsNoTracking().Where(x => x.Status == Denuncia.Recebida);
        var total = await consulta.CountAsync(cancellationToken);
        var inicio = (long)(page - 1) * pageSize;
        if (inicio >= total)
        {
            return new FilaDenuncias([], total);
        }

        var itens = await consulta.OrderBy(x => x.CriadoEm).ThenBy(x => x.Id)
            .Skip((int)inicio).Take(pageSize)
            .Select(x => new DenunciaRecebida(x.Id, x.IlustracaoId, x.Motivo, x.Descricao, x.Contato, x.CriadoEm))
            .ToListAsync(cancellationToken);

        return new FilaDenuncias(itens, total);
    }

    public async Task<ResultadoEncerramento> EncerrarAsync(Guid id, Guid moderadorId, string justificativa,
        DateTimeOffset encerradaEm, CancellationToken cancellationToken)
    {
        var afetadas = await db.Denuncias.Where(x => x.Id == id && x.Status == Denuncia.Recebida)
            .ExecuteUpdateAsync(set => set
                .SetProperty(x => x.Status, Denuncia.Encerrada)
                .SetProperty(x => x.EncerradaPorId, moderadorId)
                .SetProperty(x => x.EncerradaEm, encerradaEm)
                .SetProperty(x => x.JustificativaEncerramento, justificativa), cancellationToken);
        if (afetadas != 0)
        {
            return ResultadoEncerramento.Encerrada;
        }

        var existe = await db.Denuncias.AnyAsync(x => x.Id == id, cancellationToken);
        return existe ? ResultadoEncerramento.JaEncerrada : ResultadoEncerramento.NaoEncontrada;
    }
}
