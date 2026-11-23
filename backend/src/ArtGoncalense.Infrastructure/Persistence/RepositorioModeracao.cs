using System.Data;
using ArtGoncalense.Application.Moderacao;
using ArtGoncalense.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtGoncalense.Infrastructure.Persistence;

public sealed class RepositorioModeracao(ArtGoncalenseDbContext db) : IRepositorioModeracao
{
    public async Task<FilaModeracao> ListarPendentesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var consulta = db.Ilustracoes.AsNoTracking().Where(x => x.Status == StatusIlustracao.Pendente);
        var total = await consulta.CountAsync(cancellationToken);
        var inicio = (long)(page - 1) * pageSize;
        if (inicio >= total)
        {
            return new FilaModeracao([], total);
        }

        var itens = await consulta.OrderBy(x => x.CriadoEm).ThenBy(x => x.Id)
            .Skip((int)inicio).Take(pageSize)
            .Select(x => new PendenciaModeracao(x.Id, x.UsuarioId, x.Titulo, x.Descricao, x.Categoria, x.CriadoEm))
            .ToListAsync(cancellationToken);

        return new FilaModeracao(itens, total);
    }

    public Task<string?> BuscarChaveImagemAsync(Guid ilustracaoId, CancellationToken cancellationToken) =>
        db.Ilustracoes.AsNoTracking()
            .Where(x => x.Id == ilustracaoId
                && (x.Status == StatusIlustracao.Pendente || x.Status == StatusIlustracao.Aprovada))
            .Select(x => x.CaminhoStorage)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ResultadoModeracao> AplicarAsync(AcaoModeracao acao, StatusIlustracao estadoAnterior,
        StatusIlustracao novoEstado, CancellationToken cancellationToken)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // A atualização condicional impede que uma segunda decisão substitua a primeira.
        var afetadas = await db.Ilustracoes.Where(x => x.Id == acao.IlustracaoId && x.Status == estadoAnterior)
            .ExecuteUpdateAsync(set => set
                .SetProperty(x => x.Status, novoEstado)
                .SetProperty(x => x.AtualizadoEm, acao.CriadoEm), cancellationToken);
        if (afetadas == 0)
        {
            var existe = await db.Ilustracoes.AnyAsync(x => x.Id == acao.IlustracaoId, cancellationToken);
            return existe ? ResultadoModeracao.EstadoIncompativel : ResultadoModeracao.NaoEncontrada;
        }

        db.AcoesModeracao.Add(acao);
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return ResultadoModeracao.Aplicada;
    }
}
