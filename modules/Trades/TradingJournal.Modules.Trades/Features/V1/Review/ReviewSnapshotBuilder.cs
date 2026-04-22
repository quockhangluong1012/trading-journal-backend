using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Features.V1.Review;

public interface IReviewSnapshotBuilder
{
    Task<ReviewSnapshot> BuildAsync(
        ReviewPeriodType periodType,
        DateTime referenceDate,
        int userId,
        CancellationToken cancellationToken);
}

public sealed class ReviewSnapshotBuilder(
    ITradeDbContext context,
    IEmotionTagProvider emotionTagProvider,
    IPsychologyProvider psychologyProvider) : IReviewSnapshotBuilder
{
    public async Task<ReviewSnapshot> BuildAsync(
        ReviewPeriodType periodType,
        DateTime referenceDate,
        int userId,
        CancellationToken cancellationToken)
    {
        ReviewPeriodBounds period = ReviewPeriodCalculator.GetBounds(periodType, referenceDate);
        Dictionary<int, string> emotionNamesById = await LoadEmotionNamesAsync(cancellationToken);

        List<TradeHistory> trades = await context.TradeHistories
            .AsNoTracking()
            .Include(th => th.TradeEmotionTags)
            .Include(th => th.TradeChecklists)
                .ThenInclude(tc => tc.PretradeChecklist)
            .Include(th => th.TradeTechnicalAnalysisTags)
                .ThenInclude(tag => tag.TechnicalAnalysis)
            .Include(th => th.TradingZone)
            .AsSplitQuery()
            .Where(th => th.CreatedBy == userId)
            .Where(th => th.Status == TradeStatus.Closed && th.Pnl.HasValue)
            .Where(th => th.ClosedDate.HasValue && th.ClosedDate.Value >= period.Start && th.ClosedDate.Value <= period.End)
            .OrderByDescending(th => th.ClosedDate)
            .ToListAsync(cancellationToken);

        List<ReviewTradeInsight> tradeInsights = [.. trades.Select(trade => MapTrade(trade, emotionNamesById))];
        List<string> psychologyNotes = await psychologyProvider.GetPsychologyByPeriod(period.Start, period.End, cancellationToken);

        return new ReviewSnapshot(
            periodType,
            period.Start,
            period.End,
            ReviewSnapshotMetrics.FromTrades(tradeInsights),
            tradeInsights,
            psychologyNotes);
    }

    private async Task<Dictionary<int, string>> LoadEmotionNamesAsync(CancellationToken cancellationToken)
    {
        List<EmotionTagCacheDto> emotionTags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);

        return emotionTags
            .GroupBy(tag => tag.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);
    }

    private static ReviewTradeInsight MapTrade(
        TradeHistory trade,
        IReadOnlyDictionary<int, string> emotionNamesById)
    {
        IReadOnlyList<string> technicalThemes = [.. trade.TradeTechnicalAnalysisTags
            .Select(tag => tag.TechnicalAnalysis?.Name ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))];

        IReadOnlyList<string> checklistItems = [.. trade.TradeChecklists
            .Select(item => item.PretradeChecklist.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))];

        IReadOnlyList<string> emotionTags = [.. trade.TradeEmotionTags?
            .Select(tag => emotionNamesById.TryGetValue(tag.EmotionTagId, out string? name) ? name : string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name)) ?? []];

        return new ReviewTradeInsight(
            trade.Id,
            trade.Asset,
            trade.Position,
            trade.Pnl ?? 0,
            trade.Date,
            trade.ClosedDate ?? trade.Date,
            trade.EntryPrice,
            trade.ExitPrice,
            trade.IsRuleBroken,
            trade.RuleBreakReason,
            trade.TradingZoneId.HasValue ? trade.TradingZone.Name : null,
            trade.ConfidenceLevel,
            technicalThemes,
            emotionTags,
            checklistItems,
            trade.Notes);
    }
}

public sealed record ReviewSnapshot(
    ReviewPeriodType PeriodType,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    ReviewSnapshotMetrics Metrics,
    IReadOnlyList<ReviewTradeInsight> Trades,
    IReadOnlyList<string> PsychologyNotes);

public sealed record ReviewTradeInsight(
    int TradeId,
    string Asset,
    PositionType Position,
    decimal Pnl,
    DateTime OpenDate,
    DateTime ClosedDate,
    decimal EntryPrice,
    decimal? ExitPrice,
    bool IsRuleBroken,
    string? RuleBreakReason,
    string? TradingZone,
    ConfidenceLevel ConfidenceLevel,
    IReadOnlyList<string> TechnicalThemes,
    IReadOnlyList<string> EmotionTags,
    IReadOnlyList<string> ChecklistItems,
    string? Notes);

public sealed record ReviewSnapshotMetrics(
    int TotalTrades,
    int Wins,
    int Losses,
    decimal TotalPnl,
    decimal WinRate,
    decimal AverageWin,
    decimal AverageLoss,
    decimal BestTradePnl,
    decimal WorstTradePnl,
    decimal BestDayPnl,
    decimal WorstDayPnl,
    int LongTrades,
    int ShortTrades,
    int RuleBreakTrades,
    int HighConfidenceTrades,
    string? TopAsset,
    string? PrimaryTradingZone,
    string? DominantEmotion,
    string? TopTechnicalTheme)
{
    public static ReviewSnapshotMetrics FromTrades(IReadOnlyCollection<ReviewTradeInsight> trades)
    {
        if (trades.Count == 0)
        {
            return new ReviewSnapshotMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null, null, null);
        }

        List<ReviewTradeInsight> winningTrades = [.. trades.Where(trade => trade.Pnl > 0)];
        List<ReviewTradeInsight> losingTrades = [.. trades.Where(trade => trade.Pnl <= 0)];
        List<IGrouping<DateTime, ReviewTradeInsight>> dailyGroups = [.. trades.GroupBy(trade => trade.ClosedDate.Date)];

        int wins = winningTrades.Count;
        int losses = losingTrades.Count;
        decimal totalPnl = Math.Round(trades.Sum(trade => trade.Pnl), 2);
        decimal winRate = Math.Round((decimal)wins / trades.Count * 100, 1);

        return new ReviewSnapshotMetrics(
            TotalTrades: trades.Count,
            Wins: wins,
            Losses: losses,
            TotalPnl: totalPnl,
            WinRate: winRate,
            AverageWin: Math.Round(winningTrades.Count > 0 ? winningTrades.Average(trade => trade.Pnl) : 0, 2),
            AverageLoss: Math.Round(losingTrades.Count > 0 ? losingTrades.Average(trade => trade.Pnl) : 0, 2),
            BestTradePnl: Math.Round(trades.Max(trade => trade.Pnl), 2),
            WorstTradePnl: Math.Round(trades.Min(trade => trade.Pnl), 2),
            BestDayPnl: Math.Round(dailyGroups.Max(group => group.Sum(trade => trade.Pnl)), 2),
            WorstDayPnl: Math.Round(dailyGroups.Min(group => group.Sum(trade => trade.Pnl)), 2),
            LongTrades: trades.Count(trade => trade.Position == PositionType.Long),
            ShortTrades: trades.Count(trade => trade.Position == PositionType.Short),
            RuleBreakTrades: trades.Count(trade => trade.IsRuleBroken),
            HighConfidenceTrades: trades.Count(trade => trade.ConfidenceLevel is ConfidenceLevel.Hight or ConfidenceLevel.VeryHigh),
            TopAsset: trades
                .GroupBy(trade => trade.Asset)
                .OrderByDescending(group => group.Sum(trade => trade.Pnl))
                .ThenByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Key)
                .FirstOrDefault(),
            PrimaryTradingZone: trades
                .Where(trade => !string.IsNullOrWhiteSpace(trade.TradingZone))
                .GroupBy(trade => trade.TradingZone!)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => group.Sum(trade => trade.Pnl))
                .ThenBy(group => group.Key)
                .Select(group => group.Key)
                .FirstOrDefault(),
            DominantEmotion: GetTopTheme(trades.SelectMany(trade => trade.EmotionTags)),
            TopTechnicalTheme: GetTopTheme(trades.SelectMany(trade => trade.TechnicalThemes)));
    }

    private static string? GetTopTheme(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .FirstOrDefault();
    }
}