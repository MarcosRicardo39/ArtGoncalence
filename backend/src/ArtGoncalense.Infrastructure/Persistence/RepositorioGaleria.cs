using ArtGoncalense.Application.Ilustracoes;
using ArtGoncalense.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtGoncalense.Infrastructure.Persistence;

public sealed class RepositorioGaleria(ArtGoncalenseDbContext db) : IRepositorioGaleria
{
    private IQueryable<Ilustracao> Aprovadas => db.Ilustracoes.AsNoTracking().Where(x =>
        x.Status == StatusIlustracao.Aprovada && db.Usuarios.Any(u => u.Id == x.UsuarioId && u.Status != StatusUsuario.Suspenso));

    private IQueryable<IlustracaoPublica> Perfis(IQueryable<Ilustracao> ilustracoes) =>
        from imagem in ilustracoes
        join artista in db.Usuarios on imagem.UsuarioId equals artista.Id
        orderby imagem.CriadoEm descending, imagem.Id
        select new IlustracaoPublica(imagem.Id, imagem.Titulo, imagem.Descricao, imagem.Categoria,
            imagem.CriadoEm, artista.Id, artista.NomePublico);

    public async Task<GaleriaPublica> ListarAsync(int pagina, int tamanho, CancellationToken cancellationToken)
    {
        var total = await Aprovadas.CountAsync(cancellationToken);
        var inicio = (long)(pagina - 1) * tamanho;
        if (inicio >= total)
        {
            return new GaleriaPublica([], total);
        }

        var itens = await Perfis(Aprovadas).Skip((int)inicio).Take(tamanho).ToListAsync(cancellationToken);
        return new GaleriaPublica(itens, total);
    }

    public Task<IlustracaoPublica?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        Perfis(Aprovadas.Where(x => x.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public Task<string?> BuscarChaveAsync(Guid id, CancellationToken cancellationToken) =>
        Aprovadas.Where(x => x.Id == id).Select(x => x.CaminhoStorage).SingleOrDefaultAsync(cancellationToken);
}
