using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArtGoncalense.Tests;

public sealed class PersistenciaPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task MigrationPreservaModeloEIntegridadeReferencial()
    {
        var options = new DbContextOptionsBuilder<ArtGoncalenseDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable(PostgreSqlFactAttribute.ConnectionVariable)!)
            .Options;
        await using var db = new ArtGoncalenseDbContext(options);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var data = new DateTimeOffset(2026, 11, 23, 15, 0, 0, TimeSpan.FromHours(-3));
        var artista = new Usuario(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.test", "Artista",
            PapelUsuario.Artista, StatusUsuario.Ativo, data);
        var moderador = new Usuario(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.test", "Moderador",
            PapelUsuario.Moderador, StatusUsuario.Ativo, data);
        var ilustracao = new Ilustracao(Guid.NewGuid(), artista.Id, "Fanart", "Descrição",
            CategoriaIlustracao.Fanart, "arte.png", "ilustracoes/arte.png", "image/png", 1200, data);
        var apoio = new LinkApoio(Guid.NewGuid(), artista.Id, "https://example.test", true);
        var denuncia = new Denuncia(Guid.NewGuid(), ilustracao.Id, "Motivo", "Descrição", "Estado de teste", data);
        var acao = new AcaoModeracao(Guid.NewGuid(), ilustracao.Id, moderador.Id, TipoAcaoModeracao.Ocultar,
            "Justificativa de teste", data);

        db.AddRange(artista, moderador, ilustracao, apoio, denuncia, acao);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var salva = await db.Ilustracoes.AsNoTracking().SingleAsync(x => x.Id == ilustracao.Id);
        Assert.Equal(artista.Id, salva.UsuarioId);
        Assert.Equal(StatusIlustracao.Pendente, salva.Status);
        Assert.Equal(CategoriaIlustracao.Fanart, salva.Categoria);
        Assert.True(salva.CriadoEm.EqualsExact(data.ToUniversalTime()));
        Assert.Equal(salva.CriadoEm, salva.AtualizadoEm);
        Assert.Equal(artista.Email, (await db.Usuarios.SingleAsync(x => x.Id == artista.Id)).Email);
        Assert.Equal(artista.Id, (await db.LinksApoio.SingleAsync(x => x.Id == apoio.Id)).UsuarioId);
        Assert.Null((await db.Denuncias.SingleAsync(x => x.Id == denuncia.Id)).Contato);
        Assert.Equal(moderador.Id, (await db.AcoesModeracao.SingleAsync(x => x.Id == acao.Id)).ModeradorId);

        db.LinksApoio.Add(new LinkApoio(Guid.NewGuid(), Guid.NewGuid(), "https://example.test", true));
        var fkError = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(fkError.InnerException).SqlState);
        db.ChangeTracker.Clear();

        db.Usuarios.Add(new Usuario(Guid.NewGuid(), artista.Email, "Outro artista",
            PapelUsuario.Artista, StatusUsuario.Ativo, data));
        var emailError = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(emailError.InnerException).SqlState);
    }

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public const string ConnectionVariable = "ARTGONCALENSE_TEST_POSTGRES";

        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
            {
                Skip = $"Configure {ConnectionVariable} para executar o teste PostgreSQL.";
            }
        }
    }
}
