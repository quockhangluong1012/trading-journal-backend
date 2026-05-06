namespace TradingJournal.Shared.Dtos;

/// <summary>
/// Cross-module DTO representing a single trade for review display.
/// Produced by the Trades module, consumed by AiInsights module.
/// </summary>
public sealed record ReviewTradeDto(
    int Id,
    string Asset,
    string Position,
    decimal? Pnl,
    DateTime Date,
    DateTime? ClosedDate,
    decimal EntryPrice,
    decimal? ExitPrice,
    int ConfidenceLevel,
    string? TradingZone,
    bool IsRuleBroken,
    string? RuleBreakReason,
    string? Notes,
    List<string> EmotionTags,
    List<string> TechnicalThemes,
    List<string> ChecklistItems);

/// <summary>
/// Paginated result from the cross-module review trades query.
/// </summary>
public sealed record ReviewTradesPageDto(
    List<ReviewTradeDto> Items,
    int TotalCount,
    bool HasMore);
