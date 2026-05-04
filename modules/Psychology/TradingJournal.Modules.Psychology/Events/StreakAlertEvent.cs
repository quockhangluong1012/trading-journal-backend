using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Modules.Psychology.Events;

/// <summary>
/// Integration event published when a streak reaches a notable threshold.
/// Consumed by the Notification module to push streak-aware alerts to the user.
/// </summary>
public sealed record StreakAlertEvent(
    Guid EventId,
    int UserId,
    string StreakType,
    int StreakLength,
    decimal StreakPnl,
    bool IsNewRecord,
    string Message) : IntegrationEvent(EventId);
