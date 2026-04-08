using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

/// <summary>
/// Cross-module contract for accessing emotion tag data.
/// Implemented by the Psychology module, consumed by any module that needs emotion tags.
/// Uses cache-aside pattern: auto-populates from DB on cache miss.
/// </summary>
public interface IEmotionTagProvider
{
    Task<List<EmotionTagCacheDto>> GetEmotionTagsAsync(CancellationToken cancellationToken = default);
}
