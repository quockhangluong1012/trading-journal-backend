using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Helpers;

/// <summary>
/// Psychology module's implementation of <see cref="IEmotionTagProvider"/>.
/// Uses cache-aside pattern: returns cached data if available, otherwise queries DB and populates cache.
/// This ensures any module can access emotion tags even if the cache is cold (e.g., on startup).
/// </summary>
internal sealed class EmotionTagProvider(IPsychologyDbContext context, ICacheRepository cacheRepository) : IEmotionTagProvider
{
    public async Task<List<EmotionTagCacheDto>> GetEmotionTagsAsync(CancellationToken cancellationToken = default)
    {
        return await cacheRepository.GetOrCreateAsync<List<EmotionTagCacheDto>>(
            CacheKeys.EmotionTags,
            async ct =>
            {
                List<EmotionTag> emotionTags = await context.EmotionTags
                    .AsNoTracking()
                    .OrderBy(x => x.Name)
                    .ToListAsync(ct);

                return [.. emotionTags.Select(e => new EmotionTagCacheDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    EmotionType = e.EmotionType
                })];
            },
            expiration: TimeSpan.FromMinutes(5),
            cancellationToken: cancellationToken) ?? [];
    }
}
