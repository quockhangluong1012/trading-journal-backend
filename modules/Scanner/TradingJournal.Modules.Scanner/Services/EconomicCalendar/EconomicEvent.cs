namespace TradingJournal.Modules.Scanner.Services.EconomicCalendar;

/// <summary>
/// Represents a single economic calendar event (e.g., NFP, CPI, FOMC).
/// This is an in-memory model (not a DB entity) since we cache from external API.
/// </summary>
public sealed record EconomicEvent
{
    /// <summary>
    /// Unique identifier derived from event name + date for deduplication.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Country code (e.g., "US", "EU", "GB", "JP").
    /// </summary>
    public required string Country { get; init; }

    /// <summary>
    /// Currency affected (e.g., "USD", "EUR", "GBP").
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Name of the economic event (e.g., "Non-Farm Payrolls", "CPI", "FOMC Statement").
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Scheduled date/time in UTC.
    /// </summary>
    public required DateTime EventDateUtc { get; init; }

    /// <summary>
    /// Impact level: Low, Medium, or High.
    /// </summary>
    public required EconomicImpact Impact { get; init; }

    /// <summary>
    /// Actual released value (null if not yet released).
    /// </summary>
    public decimal? Actual { get; init; }

    /// <summary>
    /// Market consensus forecast.
    /// </summary>
    public decimal? Forecast { get; init; }

    /// <summary>
    /// Previous period value.
    /// </summary>
    public decimal? Previous { get; init; }

    /// <summary>
    /// Unit of measurement (e.g., "%", "K", "B").
    /// </summary>
    public string? Unit { get; init; }
}

/// <summary>
/// Impact severity of an economic event on the market.
/// </summary>
public enum EconomicImpact
{
    Low = 0,
    Medium = 1,
    High = 2
}
