namespace TradingJournal.Modules.Scanner.Services.EconomicCalendar;

/// <summary>
/// Provides economic calendar data with in-memory caching.
/// Implementations fetch from external APIs and cache daily.
/// </summary>
public interface IEconomicCalendarProvider
{
    /// <summary>
    /// Gets economic events for a date range. Results are cached in-memory.
    /// </summary>
    Task<List<EconomicEvent>> GetEventsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);

    /// <summary>
    /// Gets today's economic events (convenience method with aggressive caching).
    /// </summary>
    Task<List<EconomicEvent>> GetTodayEventsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets upcoming high-impact events within the specified time window.
    /// Used by the background service to trigger "stop trading" warnings.
    /// </summary>
    Task<List<EconomicEvent>> GetUpcomingHighImpactEventsAsync(
        TimeSpan lookAheadWindow,
        CancellationToken ct = default);

    /// <summary>
    /// Forces a cache refresh for today's events.
    /// </summary>
    Task RefreshTodayCacheAsync(CancellationToken ct = default);
}
