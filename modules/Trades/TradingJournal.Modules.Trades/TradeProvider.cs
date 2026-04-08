using TradingJournal.Shared.Contracts;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades;

internal sealed class TradeProvider(ITradeDbContext context, ICacheRepository cacheRepository) : ITradeProvider
{
    public async Task<List<TradeCacheDto>> GetTradesAsync(CancellationToken cancellationToken = default)
    {
        return await cacheRepository.GetOrCreateAsync<List<TradeCacheDto>>(
            CacheKeys.Trades,
            async ct =>
            {
                var trades = await context.TradeHistories
                    .AsNoTracking()
                    .Include(x => x.TradeEmotionTags)
                    .ToListAsync(ct);

                return [.. trades.Select(t => new TradeCacheDto
                {
                    Id = t.Id,
                    Asset = t.Asset,
                    Position = t.Position,
                    EntryPrice = t.EntryPrice,
                    ExitPrice = t.ExitPrice,
                    StopLoss = t.StopLoss,
                    TargetTier1 = t.TargetTier1,
                    Status = t.Status,
                    Date = t.Date,
                    Pnl = (decimal?)t.Pnl,
                    ClosedDate = t.ClosedDate,
                    TradingSessionId = t.TradingSessionId,
                    TradingZoneId = t.TradingZoneId,
                    EmotionTags = t.TradeEmotionTags?.Select(e => e.EmotionTagId).ToList() ?? [],
                    CreatedBy = t.CreatedBy
                })];
            },
            expiration: TimeSpan.FromMinutes(5),
            cancellationToken: cancellationToken) ?? [];
    }
}
