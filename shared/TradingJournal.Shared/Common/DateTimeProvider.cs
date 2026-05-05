namespace TradingJournal.Shared.Common;

/// <summary>
/// Provides current date/time as DateTime with proper timezone handling.
/// UtcNow returns UTC; Now returns Vietnam timezone (SE Asia Standard Time, UTC+7).
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime Now => TimeZoneInfo.ConvertTime(DateTime.UtcNow, VietnamTimeZone);
}
