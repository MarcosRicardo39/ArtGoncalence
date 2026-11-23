using ArtGoncalense.Application.Imagens;
using ArtGoncalense.Domain;

namespace ArtGoncalense.Application.Ilustracoes;

public enum DisponibilidadePublicacao
{
    Disponivel,
    ArtistaIndisponivel,
    LimiteAtingido
}

public sealed record ResultadoPublicacao(DisponibilidadePublicacao Situacao, Guid? Id = null);

public sealed class CommitPublicacaoIndeterminadoException(Exception causa)
    : Exception("Não foi possível confirmar o resultado do commit da publicação.", causa);

public interface IRepositorioPublicacoes
{
    Task<DisponibilidadePublicacao> VerificarDisponibilidadeAsync(Guid artistaId, CancellationToken cancellationToken);
    Task<DisponibilidadePublicacao> TentarInserirAsync(Ilustracao ilustracao, CancellationToken cancellationToken);
}

public sealed class ServicoPublicacao(
    IRepositorioPublicacoes repositorio,
    IProcessadorImagens processador,
    IArmazenamentoImagens armazenamento,
    TimeProvider relogio)
{
    public const int MaximoAtivasPorArtista = 5;

    public async Task<ResultadoPublicacao> PublicarAsync(
        Guid artistaId, string titulo, string descricao, CategoriaIlustracao categoria,
        Stream arquivo, string nomeArquivo, string tipoDeclarado, CancellationToken cancellationToken)
    {
        var disponibilidade = await repositorio.VerificarDisponibilidadeAsync(artistaId, cancellationToken);
        if (disponibilidade != DisponibilidadePublicacao.Disponivel)
        {
            return new ResultadoPublicacao(disponibilidade);
        }

        var imagem = await processador.OtimizarAsync(arquivo, nomeArquivo, tipoDeclarado, cancellationToken);
        var chave = await armazenamento.SalvarAsync(imagem, cancellationToken);
        var manterArquivo = false;
        try
        {
            var ilustracao = new Ilustracao(Guid.NewGuid(), artistaId, titulo, descricao, categoria,
                chave, chave, ImagemOtimizada.Tipo, imagem.Bytes.Length, relogio.GetUtcNow());

            disponibilidade = await repositorio.TentarInserirAsync(ilustracao, cancellationToken);
            if (disponibilidade == DisponibilidadePublicacao.Disponivel)
            {
                manterArquivo = true;
                return new ResultadoPublicacao(disponibilidade, ilustracao.Id);
            }

            return new ResultadoPublicacao(disponibilidade);
        }
        catch (CommitPublicacaoIndeterminadoException)
        {
            // A reconciliação verifica se o banco chegou a referenciar o arquivo.
            manterArquivo = true;
            throw;
        }
        finally
        {
            if (!manterArquivo)
            {
                await armazenamento.ExcluirAsync(chave, CancellationToken.None);
            }
        }
    }
}
