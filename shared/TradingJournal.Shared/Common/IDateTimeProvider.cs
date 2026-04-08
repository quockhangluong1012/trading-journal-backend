namespace TradingJournal.Shared.Common;

/// <summary>
/// Provides current date/time with proper timezone handling.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current date and time in the configured timezone.
    /// </summary>
    DateTime Now { get; }
}
