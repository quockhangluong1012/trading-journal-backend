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
                    Pnl = t.Pnl,
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

    public async Task<List<TradeCacheDto>> GetTradesInRangeAsync(int userId, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        var trades = await context.TradeHistories
            .AsNoTracking()
            .Where(x => x.CreatedBy == userId && x.Status == TradeStatus.Closed && x.ClosedDate.HasValue && x.ClosedDate.Value >= fromDate)
            .Include(x => x.TradeEmotionTags)
            .Include(x => x.TradeTechnicalAnalysisTags)
            .ToListAsync(cancellationToken);

        return [.. trades.Select(t => MapToCacheDto(t))];
    }

    public async Task<TradeStatisticsDto> GetTradeStatisticsAsync(int userId, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        var closedTrades = context.TradeHistories
            .AsNoTracking()
            .Where(x => x.CreatedBy == userId && x.Status == TradeStatus.Closed && x.Pnl.HasValue)
            .Where(x => fromDate == DateTime.MinValue || (x.ClosedDate.HasValue && x.ClosedDate.Value >= fromDate));

        var openCount = await context.TradeHistories
            .AsNoTracking()
            .Where(x => x.CreatedBy == userId && x.Status == TradeStatus.Open && !x.Pnl.HasValue)
            .CountAsync(cancellationToken);

        var totalTrades = await context.TradeHistories
            .AsNoTracking()
            .Where(x => x.CreatedBy == userId)
            .Where(x => fromDate == DateTime.MinValue || x.Date >= fromDate)
            .CountAsync(cancellationToken);

        return new TradeStatisticsDto
        {
            TotalPnl = await closedTrades.SumAsync(x => (decimal?)x.Pnl ?? 0, cancellationToken),
            WinCount = await closedTrades.CountAsync(x => x.Pnl > 0, cancellationToken),
            LossCount = await closedTrades.CountAsync(x => x.Pnl < 0, cancellationToken),
            OpenPositions = openCount,
            TotalTrades = totalTrades,
            ClosedTrades = await closedTrades.CountAsync(cancellationToken)
        };
    }

    public async Task<List<AssetBreakdownDto>> GetAssetBreakdownsAsync(int userId, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        var query = context.TradeHistories
            .AsNoTracking()
            .Where(x => x.CreatedBy == userId && x.Status == TradeStatus.Closed && x.Pnl.HasValue && !string.IsNullOrEmpty(x.Asset))
            .Where(x => fromDate == DateTime.MinValue || (x.ClosedDate.HasValue && x.ClosedDate.Value >= fromDate));

        var breakdowns = await query
            .GroupBy(x => x.Asset)
            .Select(g => new AssetBreakdownDto
            {
                Asset = g.Key,
                TotalPnl = g.Sum(x => (decimal)x.Pnl!),
                TradeCount = g.Count(),
                WinCount = g.Count(x => x.Pnl > 0)
            })
            .ToListAsync(cancellationToken);

        return breakdowns
            .OrderByDescending(a => Math.Abs(a.TotalPnl))
            .ThenByDescending(a => a.TradeCount)
            .ThenBy(a => a.Asset)
            .ToList();
    }

    public async Task<List<MonthlyPnlDto>> GetMonthlyPnlAsync(int userId, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        var closedTrades = await context.TradeHistories
            .AsNoTracking()
            .Where(x => x.CreatedBy == userId && x.Status == TradeStatus.Closed && x.Pnl.HasValue && x.ClosedDate.HasValue)
            .Where(x => fromDate == DateTime.MinValue || x.ClosedDate!.Value >= fromDate)
            .Select(x => new { x.ClosedDate, x.Pnl })
            .ToListAsync(cancellationToken);

        // LINQ-to-Objects for month formatting (EF can't translate .ToString("yyyy-MM") reliably across providers)
        return closedTrades
            .GroupBy(x => $"{x.ClosedDate!.Value.Year}-{x.ClosedDate.Value.Month:D2}")
            .Select(g => new MonthlyPnlDto
            {
                Month = g.Key,
                Pnl = Math.Round(g.Sum(x => (decimal)x.Pnl!.Value), 2)
            })
            .OrderBy(m => m.Month)
            .ToList();
    }

    private static TradeCacheDto MapToCacheDto(TradeHistory t) => new()
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
        Pnl = t.Pnl,
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
    };
}

