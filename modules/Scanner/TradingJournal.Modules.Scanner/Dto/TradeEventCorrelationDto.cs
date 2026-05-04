namespace TradingJournal.Modules.Scanner.Dto;

/// <summary>
/// Response DTO for trade-event correlation analysis.
/// Shows how trades perform relative to high-impact economic events.
/// </summary>
public sealed record TradeEventCorrelationDto(
    /// <summary>Total trades analyzed in the period.</summary>
    int TotalTradesAnalyzed,

    /// <summary>Trades opened within the event proximity window.</summary>
    int TradesNearEvents,

    /// <summary>Trades opened outside the event proximity window.</summary>
    int TradesAwayFromEvents,

    /// <summary>Win rate for trades opened near events (0-100).</summary>
    decimal WinRateNearEvents,

    /// <summary>Win rate for trades opened away from events (0-100).</summary>
    decimal WinRateAwayFromEvents,

    /// <summary>Average PnL for trades near events.</summary>
    decimal AvgPnlNearEvents,

    /// <summary>Average PnL for trades away from events.</summary>
    decimal AvgPnlAwayFromEvents,

    /// <summary>Total PnL for trades near events.</summary>
    decimal TotalPnlNearEvents,

    /// <summary>Total PnL for trades away from events.</summary>
    decimal TotalPnlAwayFromEvents,

    /// <summary>Breakdown by specific event type (e.g., NFP, CPI, FOMC).</summary>
    List<EventTypeCorrelationDto> EventTypeBreakdown,

    /// <summary>Breakdown by currency affected.</summary>
    List<CurrencyCorrelationDto> CurrencyBreakdown,

    /// <summary>Breakdown by time proximity bucket (before/during/after).</summary>
    List<ProximityBucketDto> ProximityBreakdown,

    /// <summary>AI-generated summary of the correlation findings.</summary>
    string Summary
);

/// <summary>
/// Correlation stats for a specific event type (e.g., "Non-Farm Payrolls").
/// </summary>
public sealed record EventTypeCorrelationDto(
    string EventName,
    int TradeCount,
    int Wins,
    int Losses,
    decimal WinRate,
    decimal AvgPnl,
    decimal TotalPnl
);

/// <summary>
/// Correlation stats grouped by currency.
/// </summary>
public sealed record CurrencyCorrelationDto(
    string Currency,
    int TradeCount,
    decimal WinRate,
    decimal AvgPnl,
    decimal TotalPnl
);

/// <summary>
/// Correlation stats by time proximity to the event.
/// </summary>
public sealed record ProximityBucketDto(
    /// <summary>Label like "0-15 min before", "15-30 min before", "0-15 min after", etc.</summary>
    string Label,

    /// <summary>Minutes from event (negative = before, positive = after).</summary>
    int MinutesFromEvent,

    int TradeCount,
    decimal WinRate,
    decimal AvgPnl
);

/// <summary>
/// Response DTO for pre-trade safety check.
/// </summary>
public sealed record PreTradeCheckDto(
    /// <summary>Whether it's currently safe to trade.</summary>
    bool IsSafeToTrade,

    /// <summary>Safety level: Green, Yellow, Red.</summary>
    string SafetyLevel,

    /// <summary>Human-readable warning/info message.</summary>
    string Message,

    /// <summary>Upcoming high-impact events within the danger window.</summary>
    List<EconomicEventDto> UpcomingDangerEvents,

    /// <summary>Recently released events still causing volatility.</summary>
    List<EconomicEventDto> RecentReleases,

    /// <summary>Minutes until next high-impact event (null if none today).</summary>
    int? MinutesUntilNextHighImpact,

    /// <summary>Recommended wait time in minutes (0 if safe to trade).</summary>
    int RecommendedWaitMinutes
);

/// <summary>
/// A single equity curve point enriched with economic event overlay data.
/// </summary>
public sealed record EquityEventOverlayPointDto(
    DateTimeOffset Date,
    decimal Profit,
    /// <summary>High-impact events that occurred on this date (may be empty).</summary>
    List<EventMarkerDto> EventMarkers
);

/// <summary>
/// Compact marker for an economic event on the equity curve.
/// </summary>
public sealed record EventMarkerDto(
    string EventName,
    string Currency,
    string Impact,
    DateTimeOffset EventDateUtc,
    decimal? Actual,
    decimal? Forecast,
    decimal? Previous
);

/// <summary>
/// Response DTO for equity curve with event overlays.
/// </summary>
public sealed record EquityCurveWithEventsDto(
    List<EquityEventOverlayPointDto> EquityPoints,
    List<EventMarkerDto> AllHighImpactEvents,
    int TotalTrades,
    int HighImpactEventsInPeriod
);
