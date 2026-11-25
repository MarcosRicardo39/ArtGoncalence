using ArtGoncalense.Domain;
using ArtGoncalense.Infrastructure.Identidade;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ArtGoncalense.Infrastructure.Persistence;

public sealed class ArtGoncalenseDbContext(DbContextOptions<ArtGoncalenseDbContext> options)
    : IdentityUserContext<ContaIdentity, Guid>(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<LinkApoio> LinksApoio => Set<LinkApoio>();
    public DbSet<Ilustracao> Ilustracoes => Set<Ilustracao>();
    public DbSet<Denuncia> Denuncias => Set<Denuncia>();
    public DbSet<AcaoModeracao> AcoesModeracao => Set<AcaoModeracao>();
    public DbSet<PedidoExclusao> PedidosExclusao => Set<PedidoExclusao>();
    public DbSet<ChaveProtecao> ChavesProtecao => Set<ChaveProtecao>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DataUtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<PedidoExclusao>().HasIndex(x => new { x.TitularId, x.ConcluidoEm });

        var usuario = modelBuilder.Entity<Usuario>();
        usuario.HasKey(x => x.Id);
        usuario.Property(x => x.Id).ValueGeneratedNever();
        usuario.Property(x => x.Email);
        usuario.Property(x => x.NomePublico);
        usuario.Property(x => x.Bio);
        usuario.Property(x => x.AvatarPath);
        usuario.Property(x => x.Papel);
        usuario.Property(x => x.Status);
        usuario.Property(x => x.CriadoEm);
        usuario.HasIndex(x => x.Email).IsUnique();

        var conta = modelBuilder.Entity<ContaIdentity>();
        conta.Property(x => x.Id).ValueGeneratedNever();
        conta.Property(x => x.Email).IsRequired();
        conta.Property(x => x.NormalizedEmail).IsRequired();
        conta.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("EmailIndex");
        conta.HasOne<Usuario>().WithOne().HasForeignKey<ContaIdentity>(x => x.Id)
            .OnDelete(DeleteBehavior.Restrict);

        var linkApoio = modelBuilder.Entity<LinkApoio>();
        linkApoio.HasKey(x => x.Id);
        linkApoio.Property(x => x.Id).ValueGeneratedNever();
        linkApoio.Property(x => x.Url);
        linkApoio.Property(x => x.Ativo);
        linkApoio.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        var ilustracao = modelBuilder.Entity<Ilustracao>();
        ilustracao.HasKey(x => x.Id);
        ilustracao.Property(x => x.Id).ValueGeneratedNever();
        ilustracao.Property(x => x.Titulo);
        ilustracao.Property(x => x.Descricao);
        ilustracao.Property(x => x.Categoria);
        ilustracao.Property(x => x.NomeArquivo);
        ilustracao.Property(x => x.CaminhoStorage);
        ilustracao.Property(x => x.MimeType);
        ilustracao.Property(x => x.TamanhoBytes);
        ilustracao.Property(x => x.CriadoEm);
        ilustracao.Property(x => x.Status);
        ilustracao.Property(x => x.AtualizadoEm);
        ilustracao.ToTable(t => t.HasCheckConstraint("CK_Ilustracoes_TamanhoBytes_Positivo", "\"TamanhoBytes\" > 0"));
        ilustracao.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        var denuncia = modelBuilder.Entity<Denuncia>();
        denuncia.HasKey(x => x.Id);
        denuncia.Property(x => x.Id).ValueGeneratedNever();
        denuncia.Property(x => x.Contato);
        denuncia.Property(x => x.Motivo);
        denuncia.Property(x => x.Descricao);
        denuncia.Property(x => x.Status);
        denuncia.Property(x => x.CriadoEm);
        denuncia.HasOne<Usuario>().WithMany().HasForeignKey(x => x.EncerradaPorId)
            .OnDelete(DeleteBehavior.Restrict);
        denuncia.HasOne<Ilustracao>().WithMany().HasForeignKey(x => x.IlustracaoId)
            .OnDelete(DeleteBehavior.Restrict);

        var acaoModeracao = modelBuilder.Entity<AcaoModeracao>();
        acaoModeracao.HasKey(x => x.Id);
        acaoModeracao.Property(x => x.Id).ValueGeneratedNever();
        acaoModeracao.Property(x => x.Acao);
        acaoModeracao.Property(x => x.Justificativa);
        acaoModeracao.Property(x => x.CriadoEm);
        acaoModeracao.HasOne<Ilustracao>().WithMany().HasForeignKey(x => x.IlustracaoId)
            .OnDelete(DeleteBehavior.Restrict);
        acaoModeracao.HasOne<Usuario>().WithMany().HasForeignKey(x => x.ModeradorId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private sealed class DataUtcConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
        data => data.ToUniversalTime(), data => data);
}
