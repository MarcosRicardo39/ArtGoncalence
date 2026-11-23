using Microsoft.Extensions.Caching.Memory;

namespace ArtGoncalense.Infrastructure.Identidade;

public sealed class LimiteEnvioConta(int limite, TimeProvider relogio) : IDisposable
{
    private static readonly TimeSpan Janela = TimeSpan.FromHours(1);
    private readonly MemoryCache cache = new(new MemoryCacheOptions());
    private readonly object sincronizacao = new();

    public bool TentarConsumir(string emailNormalizado)
    {
        lock (sincronizacao)
        {
            var agora = relogio.GetUtcNow();
            var envios = cache.Get<Queue<DateTimeOffset>>(emailNormalizado) ?? new Queue<DateTimeOffset>();
            while (envios.TryPeek(out var envio) && envio <= agora - Janela)
            {
                envios.Dequeue();
            }

            if (envios.Count >= limite)
            {
                return false;
            }

            envios.Enqueue(agora);
            cache.Set(emailNormalizado, envios, Janela);
            return true;
        }
    }

    public void Dispose() => cache.Dispose();
}
