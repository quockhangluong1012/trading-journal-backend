using Microsoft.Extensions.Caching.Hybrid;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Shared.Repositories;

public class CacheRepository(HybridCache hybridCache) : ICacheRepository
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromSeconds(30);

    public async Task<T?> GetOrCreateAsync<T>(string key,
        Func<CancellationToken, Task<T>> handle,
        TimeSpan? expiration,
        CancellationToken cancellationToken = default)
    {
        TimeSpan? expiredTime = expiration ?? DefaultExpiration;

        HybridCacheEntryOptions entryOptions = new()
        {
            Expiration = expiredTime,
            LocalCacheExpiration = expiredTime
        };

        T? result = await hybridCache.GetOrCreateAsync<T>(
            key,
            async (entry) => await handle(cancellationToken),
            entryOptions,
            tags: [],
            cancellationToken: cancellationToken);

        return result;
    }

    public async Task UpdateCache<T>(string key, Func<CancellationToken, Task<T>> handle, TimeSpan? expiration, CancellationToken cancellationToken = default)
    {
        await hybridCache.RemoveAsync(key, cancellationToken);

        TimeSpan? expiredTime = expiration ?? DefaultExpiration;

        HybridCacheEntryOptions entryOptions = new()
        {
            Expiration = expiredTime,
            LocalCacheExpiration = expiredTime
        };

        T? value = await handle(cancellationToken);

        await hybridCache.SetAsync(key, value, entryOptions, cancellationToken: cancellationToken);
    }

    public async Task RemoveCache(string key, CancellationToken cancellationToken = default)
    {
        await hybridCache.RemoveAsync(key, cancellationToken);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        T? result = await hybridCache.GetOrCreateAsync<T>(
            key,
            async (entry) => default(T)!,
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromSeconds(0),
                LocalCacheExpiration = TimeSpan.FromSeconds(0)
            },
            tags: [],
            cancellationToken: cancellationToken);

        return result;
    }
}