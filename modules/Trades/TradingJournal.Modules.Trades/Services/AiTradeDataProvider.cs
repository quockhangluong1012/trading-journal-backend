using TradingJournal.Shared.Common;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Features.V1.Review;

namespace TradingJournal.Modules.Trades.Services;

/// <summary>
/// Implements IAiTradeDataProvider for cross-module access.
/// AiInsights module resolves this via DI to read trade data without
/// directly referencing TradeDbContext or Trades domain entities.
/// </summary>
internal sealed class AiTradeDataProvider(
    ITradeDbContext context,
    IReviewSnapshotBuilder snapshotBuilder,
    IEmotionTagProvider emotionTagProvider,
    IPsychologyProvider psychologyProvider) : IAiTradeDataProvider
{
    public async Task<AiTradeDetailDto> GetTradeDetailForAiAsync(int tradeHistoryId, CancellationToken cancellationToken)
    {
        var trade = await context.TradeHistories
            .AsNoTracking()
            .Include(th => th.TradeEmotionTags)
            .Include(th => th.TradeChecklists)
                .ThenInclude(tc => tc.PretradeChecklist)
            .Include(th => th.TradeTechnicalAnalysisTags)
                .ThenInclude(tag => tag.TechnicalAnalysis)
            .Include(th => th.TradingZone)
            .Include(th => th.TradeScreenShots)
            .AsSplitQuery()
            .FirstOrDefaultAsync(th => th.Id == tradeHistoryId, cancellationToken)
            ?? throw new InvalidOperationException($"Trade history not found: {tradeHistoryId}");

        // Resolve emotion tag names
        List<EmotionTagCacheDto> allEmotionTags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);
        Dictionary<int, string> emotionNamesById = allEmotionTags
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        List<string> emotionTagNames = [.. trade.TradeEmotionTags?
            .Select(t => emotionNamesById.TryGetValue(t.EmotionTagId, out string? name) ? name : string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n)) ?? []];

        // Resolve checklist names
        List<string> checklistNames = [.. trade.TradeChecklists
            .Select(c => c.PretradeChecklist.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))];

        // Resolve technical analysis tag names
        List<string> technicalTags = [.. trade.TradeTechnicalAnalysisTags
            .Select(t => t.TechnicalAnalysis?.Name ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))];

        // Screenshot URLs
        List<string> screenshotUrls = [.. trade.TradeScreenShots
            .Select(s => s.Url)
            .Where(u => !string.IsNullOrWhiteSpace(u))];

        // Psychology notes
        List<string> psychologyNotes = await psychologyProvider
            .GetPsychologyByDate(trade.Date, cancellationToken);

        return new AiTradeDetailDto
        {
            TradeHistoryId = trade.Id,
            Asset = trade.Asset,
            Position = trade.Position.ToString(),
            EntryPrice = trade.EntryPrice,
            TargetTier1 = trade.TargetTier1,
            TargetTier2 = trade.TargetTier2,
            TargetTier3 = trade.TargetTier3,
            StopLoss = trade.StopLoss,
            Notes = trade.Notes ?? string.Empty,
            ExitPrice = trade.ExitPrice,
            Pnl = trade.Pnl,
            ConfidenceLevel = trade.ConfidenceLevel.ToString(),
            TradingZone = trade.TradingZone?.Name ?? string.Empty,
            OpenDate = trade.Date,
            ClosedDate = trade.ClosedDate ?? trade.Date,
            TechnicalAnalysisTags = technicalTags,
            EmotionTags = emotionTagNames,
            ChecklistItems = checklistNames,
            ScreenshotUrls = screenshotUrls,
            PsychologyNotes = psychologyNotes,
        };
    }

    public Task<ReviewSnapshot> BuildReviewSnapshotAsync(
        ReviewPeriodType periodType,
        DateTimeOffset referenceDate,
        int userId,
        CancellationToken cancellationToken)
    {
        return snapshotBuilder.BuildAsync(periodType, referenceDate, userId, cancellationToken);
    }

    public async Task UpdateTradeSummaryIdAsync(int tradeHistoryId, int summaryId, CancellationToken cancellationToken)
    {
        var trade = await context.TradeHistories
            .FirstOrDefaultAsync(th => th.Id == tradeHistoryId, cancellationToken);

        if (trade is not null)
        {
            trade.TradingSummaryId = summaryId;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
