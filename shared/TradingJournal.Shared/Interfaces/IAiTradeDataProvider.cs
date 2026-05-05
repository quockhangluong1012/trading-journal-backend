using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

/// <summary>
/// Cross-module contract for accessing trade data needed by the AiInsights module.
/// Implemented by the Trades module, consumed by AiInsights for AI analysis.
/// </summary>
public interface IAiTradeDataProvider
{
    /// <summary>
    /// Loads full trade detail with all related data (screenshots, emotions, checklists, TA tags, zone, psychology)
    /// pre-resolved into a flat DTO for AI analysis.
    /// </summary>
    Task<AiTradeDetailDto> GetTradeDetailForAiAsync(int tradeHistoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Builds a complete review snapshot for AI review generation.
    /// </summary>
    Task<ReviewSnapshot> BuildReviewSnapshotAsync(
        ReviewPeriodType periodType,
        DateTime referenceDate,
        int userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the TradingSummaryId on a TradeHistory after AI generates a summary.
    /// </summary>
    Task UpdateTradeSummaryIdAsync(int tradeHistoryId, int summaryId, CancellationToken cancellationToken);
}
