using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArtGoncalense.Infrastructure.Persistence;

public sealed class RepositorioChavesProtecao(IServiceScopeFactory escopos) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var escopo = escopos.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        return db.ChavesProtecao.AsNoTracking().Select(chave => chave.Xml).ToList()
            .Select(xml => XElement.Parse(xml)).ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var escopo = escopos.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ArtGoncalenseDbContext>();
        db.ChavesProtecao.Add(new ChaveProtecao
        {
            Nome = friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting)
        });
        db.SaveChanges();
    }
}
