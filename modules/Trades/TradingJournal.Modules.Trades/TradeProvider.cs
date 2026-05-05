using TradingJournal.Shared.Contracts;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades;

internal sealed class TradeProvider(ITradeDbContext context, ICacheRepository cacheRepository) : ITradeProvider
{
    public async Task<List<TradeCacheDto>> GetTradesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await cacheRepository.GetOrCreateAsync<List<TradeCacheDto>>(
            CacheKeys.TradesForUser(userId),
            async ct =>
            {
                var trades = await context.TradeHistories
                    .AsNoTracking()
                    .Where(x => x.CreatedBy == userId)
                    .Include(x => x.TradeEmotionTags)
                    .Include(x => x.TradeTechnicalAnalysisTags)
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
                    TradingSetupId = t.TradingSetupId,
                    TechnicalAnalysisTagIds = t.TradeTechnicalAnalysisTags?.Select(ta => ta.TechnicalAnalysisId).ToList() ?? [],
                    IsRuleBroken = t.IsRuleBroken,
                    CreatedBy = t.CreatedBy,
                    PowerOf3Phase = t.PowerOf3Phase.HasValue ? (int)t.PowerOf3Phase.Value : null,
                    DailyBias = t.DailyBias.HasValue ? (int)t.DailyBias.Value : null,
                    MarketStructure = t.MarketStructure.HasValue ? (int)t.MarketStructure.Value : null,
                    PremiumDiscount = t.PremiumDiscount.HasValue ? (int)t.PremiumDiscount.Value : null
                })];
            },
            expiration: TimeSpan.FromMinutes(5),
            cancellationToken: cancellationToken) ?? [];
    }

    public async Task<List<TradeCacheDto>> GetRecentTradesAsync(int userId, DateTime since, CancellationToken cancellationToken = default)
    {
        var allTrades = await GetTradesAsync(userId, cancellationToken);
        return allTrades
            .Where(t => t.Date >= since)
            .OrderByDescending(t => t.Date)
            .ToList();
    }

    public async Task<List<TradeCacheDto>> GetClosedTradesDescendingAsync(int userId, int count, CancellationToken cancellationToken = default)
    {
        var allTrades = await GetTradesAsync(userId, cancellationToken);
        return allTrades
            .Where(t => t.Status == TradeStatus.Closed && t.ClosedDate.HasValue)
            .OrderByDescending(t => t.ClosedDate)
            .Take(count)
            .ToList();
    }
}

