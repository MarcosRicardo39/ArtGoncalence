using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtGoncalense.Infrastructure.Imagens;

public sealed class ReconciliadorImagens(
    ArmazenamentoImagensLocal armazenamento,
    ArtGoncalenseDbContext db,
    TimeProvider relogio)
{
    public async Task<int> LimparOrfaosAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var anteriorA = relogio.GetUtcNow().AddHours(-24);
        var removidos = 0;

        foreach (var chave in armazenamento.ListarChavesAntigas(anteriorA))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var referenciada = await db.Ilustracoes.AnyAsync(x => x.CaminhoStorage == chave, cancellationToken)
                || await db.Usuarios.AnyAsync(x => x.AvatarPath == chave, cancellationToken);
            if (!referenciada)
            {
                await armazenamento.ExcluirAsync(chave, cancellationToken);
                removidos++;
            }
        }

        return removidos + armazenamento.LimparTemporariosAntigos(anteriorA, cancellationToken);
    }
}
