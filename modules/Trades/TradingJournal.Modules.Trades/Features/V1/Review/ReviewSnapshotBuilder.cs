using TradingJournal.Shared.Common;
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
