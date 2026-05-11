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
    /// Builds the trade and lesson context needed for AI lesson suggestion generation.
    /// </summary>
    Task<LessonSuggestionContextDto> GetLessonSuggestionContextAsync(
        DateTime? fromDate,
        DateTime? toDate,
        int userId,
        int maxTrades,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns user-authored lessons that are most relevant to the current AI research question.
    /// </summary>
    Task<LessonKnowledgeContextDto> GetLessonKnowledgeContextAsync(
        int userId,
        string? focusQuery,
        int maxLessons,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the user's saved lessons, playbooks, and daily notes that are most relevant to the current AI research question.
    /// </summary>
    Task<ResearchKnowledgeContextDto> GetResearchKnowledgeContextAsync(
        int userId,
        string? focusQuery,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves paginated trades within a date range for review display.
    /// </summary>
    Task<ReviewTradesPageDto> GetReviewTradesAsync(
        DateTime fromDate, DateTime toDate, int userId,
        int page, int pageSize, CancellationToken cancellationToken);
}


