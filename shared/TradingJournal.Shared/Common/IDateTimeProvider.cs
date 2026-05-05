namespace TradingJournal.Shared.Common;

/// <summary>
/// Provides current date/time with proper timezone handling.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time as DateTime.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current date and time in the configured timezone (Vietnam, UTC+7).
    /// </summary>
    DateTime Now { get; }
}
