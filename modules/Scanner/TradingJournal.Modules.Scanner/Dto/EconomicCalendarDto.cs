using TradingJournal.Modules.Scanner.Services.EconomicCalendar;

namespace TradingJournal.Modules.Scanner.Dto;

/// <summary>
/// DTO for an economic calendar event returned by the API.
/// </summary>
public sealed record EconomicEventDto(
    string Id,
    string Country,
    string Currency,
    string EventName,
    DateTime EventDateUtc,
    string Impact,
    decimal? Actual,
    decimal? Forecast,
    decimal? Previous,
    string? Unit,
    bool IsUpcoming,
    int? MinutesUntilRelease);

/// <summary>
/// Response DTO for the economic calendar endpoint.
/// </summary>
public sealed record EconomicCalendarDto(
    DateOnly From,
    DateOnly To,
    int TotalEvents,
    int HighImpactCount,
    int MediumImpactCount,
    int LowImpactCount,
    List<EconomicEventDto> Events);

/// <summary>
/// Response DTO for upcoming high-impact events.
/// </summary>
public sealed record UpcomingHighImpactDto(
    int Count,
    bool ShouldStopTrading,
    string? NextEventName,
    int? MinutesUntilNext,
    List<EconomicEventDto> Events);
