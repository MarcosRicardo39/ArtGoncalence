using System.Data;
using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Identidade;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtGoncalense.Infrastructure.Persistence;

public sealed class Privacidade(
    ArtGoncalenseDbContext db, UserManager<ContaIdentity> usuarios, SignInManager<ContaIdentity> sessoes,
    TimeProvider relogio)
{
    public async Task<(int Status, Guid? Id)> SolicitarAsync(Guid titular, Guid? ilustracao, string senha,
        CancellationToken ct)
    {
        var conta = await usuarios.FindByIdAsync(titular.ToString());
        if (conta is null || !(await sessoes.CheckPasswordSignInAsync(conta, senha, lockoutOnFailure: true)).Succeeded)
        {
            return (403, null);
        }

        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM "Usuarios" WHERE "Id" = {titular} FOR NO KEY UPDATE
            """, ct);
        var artista = db.Usuarios.Where(x => x.Id == titular && x.Papel == PapelUsuario.Artista);
        if (!await artista.AnyAsync(ct))
        {
            return (404, null);
        }

        var existente = await db.PedidosExclusao.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TitularId == titular && x.IlustracaoId == ilustracao && x.ConcluidoEm == null, ct);
        if (existente is not null)
        {
            return (202, existente.Id);
        }

        var agora = relogio.GetUtcNow();
        var obras = db.Ilustracoes.Where(x => x.UsuarioId == titular);
        if (ilustracao is Guid id)
        {
            var alteradas = await obras.Where(x => x.Id == id).ExecuteUpdateAsync(set => set
                .SetProperty(x => x.Status, StatusIlustracao.Oculta).SetProperty(x => x.AtualizadoEm, agora), ct);
            if (alteradas == 0)
            {
                return (404, null);
            }
        }
        else
        {
            await artista.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, StatusUsuario.Suspenso), ct);
            await obras.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, StatusIlustracao.Oculta)
                .SetProperty(x => x.AtualizadoEm, agora), ct);
        }

        var pedido = new PedidoExclusao(Guid.NewGuid(), titular, ilustracao, agora);
        db.PedidosExclusao.Add(pedido);
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        if (ilustracao is null)
        {
            await sessoes.SignOutAsync();
        }

        return (202, pedido.Id);
    }

    public Task<List<PedidoExclusao>> ListarAsync(CancellationToken ct) =>
        db.PedidosExclusao.AsNoTracking().Where(x => x.ConcluidoEm == null)
            .OrderBy(x => x.SolicitadoEm).ThenBy(x => x.Id).Take(100).ToListAsync(ct);

    public async Task<ResultadoExclusao> ProcessarAsync(Guid id, IArmazenamentoImagens armazenamento, CancellationToken ct)
    {
        var titular = await db.PedidosExclusao.Where(x => x.Id == id)
            .Select(x => (Guid?)x.TitularId).SingleOrDefaultAsync(ct);
        if (titular is null)
        {
            return ResultadoExclusao.Inexistente;
        }

        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM "Usuarios" WHERE "Id" = {titular.Value} FOR NO KEY UPDATE
            """, ct);
        var pedido = await db.PedidosExclusao.SingleAsync(x => x.Id == id, ct);
        if (pedido.ConcluidoEm is not null)
        {
            return ResultadoExclusao.Concluida;
        }

        var obras = db.Ilustracoes.Where(x => x.UsuarioId == pedido.TitularId
            && (pedido.IlustracaoId == null || x.Id == pedido.IlustracaoId));
        var ids = await obras.Select(x => x.Id).ToArrayAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM "Denuncias" WHERE "IlustracaoId" = ANY ({ids}) FOR UPDATE
            """, ct);
        var contaInteira = pedido.IlustracaoId is null;
        if (await db.Denuncias.AnyAsync(x => ids.Contains(x.IlustracaoId)
                && (x.Status == Denuncia.Recebida || x.MotivoConservacaoContato != null), ct)
            || (contaInteira && (
                await db.Denuncias.AnyAsync(x => x.EncerradaPorId == pedido.TitularId && !ids.Contains(x.IlustracaoId), ct)
                || await db.AcoesModeracao.AnyAsync(x => x.ModeradorId == pedido.TitularId && !ids.Contains(x.IlustracaoId), ct))))
        {
            return ResultadoExclusao.Conservacao;
        }

        var chaves = await obras.Select(x => x.CaminhoStorage).Distinct().ToListAsync(ct);
        if (contaInteira)
        {
            var avatar = await db.Usuarios.Where(x => x.Id == pedido.TitularId).Select(x => x.AvatarPath)
                .SingleOrDefaultAsync(ct);
            if (avatar is not null) chaves.Add(avatar);
        }

        foreach (var chave in chaves.Distinct())
        {
            var compartilhada = await db.Ilustracoes.AnyAsync(x => x.CaminhoStorage == chave && !ids.Contains(x.Id), ct)
                || await db.Usuarios.AnyAsync(x => x.AvatarPath == chave && (!contaInteira || x.Id != pedido.TitularId), ct);
            if (!compartilhada)
            {
                await armazenamento.ExcluirAsync(chave, ct);
            }
        }

        await db.Denuncias.Where(x => ids.Contains(x.IlustracaoId)).ExecuteDeleteAsync(ct);
        await db.AcoesModeracao.Where(x => ids.Contains(x.IlustracaoId)).ExecuteDeleteAsync(ct);
        await obras.ExecuteDeleteAsync(ct);
        if (contaInteira)
        {
            var dono = pedido.TitularId;
            await db.LinksApoio.Where(x => x.UsuarioId == dono).ExecuteDeleteAsync(ct);
            await db.Users.Where(x => x.Id == dono).ExecuteDeleteAsync(ct);
            await db.Usuarios.Where(x => x.Id == dono).ExecuteDeleteAsync(ct);
            var pedidosRelacionados = await db.PedidosExclusao
                .Where(x => x.TitularId == dono && x.Id != pedido.Id && x.ConcluidoEm == null).ToListAsync(ct);
            foreach (var relacionado in pedidosRelacionados) relacionado.Concluir(relogio.GetUtcNow());
        }

        pedido.Concluir(relogio.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return ResultadoExclusao.Concluida;
    }

    public Task<int> LimparContatosAsync(CancellationToken ct)
    {
        var corte = relogio.GetUtcNow().AddDays(-90);
        return db.Denuncias.Where(x => x.Status == Denuncia.Encerrada && x.EncerradaEm <= corte
            && x.Contato != null && x.MotivoConservacaoContato == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Contato, (string?)null), ct);
    }

    public Task<int> RegistrarConservacaoAsync(Guid denunciaId, string? motivo, CancellationToken ct) =>
        db.Denuncias.Where(x => x.Id == denunciaId && x.Contato != null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.MotivoConservacaoContato, motivo), ct);
}

public enum ResultadoExclusao
{
    Inexistente,
    Concluida,
    Conservacao
}
