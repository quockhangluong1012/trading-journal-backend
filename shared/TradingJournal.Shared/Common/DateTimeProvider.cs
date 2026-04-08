namespace TradingJournal.Shared.Common;

/// <summary>
/// Provides current date/time converted to Vietnam timezone (SE Asia Standard Time, UTC+7).
/// Uses TimeZoneInfo instead of manual offset for correctness.
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
}
