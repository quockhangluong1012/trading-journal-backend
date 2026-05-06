namespace TradingJournal.Shared.Dtos;

public sealed record EconomicImpactEventDto(
    string EventId,
    string EventName,
    string Country,
    string Currency,
    string Impact,
    DateTime EventDateUtc,
    int? MinutesUntilRelease,
    decimal? Forecast,
    decimal? Previous);

public sealed record EconomicImpactContextDto(
    string Symbol,
    string SafetyLevel,
    string SafetyMessage,
    int? MinutesUntilNextHighImpactEvent,
    int RecommendedWaitMinutes,
    int TradesNearEvents,
    int TradesAwayFromEvents,
    decimal WinRateNear,
    decimal WinRateAway,
    decimal AvgPnlNear,
    decimal AvgPnlAway,
    string CorrelationSummary,
    IReadOnlyList<EconomicImpactEventDto> UpcomingEvents);