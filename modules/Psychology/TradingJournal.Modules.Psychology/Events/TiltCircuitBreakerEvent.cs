using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Modules.Psychology.Events;

/// <summary>
/// Integration event published when a tilt circuit breaker is triggered.
/// Consumed by the Notification module to push an alert to the user.
/// </summary>
public sealed record TiltCircuitBreakerEvent(
    Guid EventId,
    int UserId,
    int TiltScore,
    string TiltLevel,
    int ConsecutiveLosses,
    int TradesLastHour,
    int RuleBreaksToday,
    decimal TodayPnl,
    DateTimeOffset CooldownUntil) : IntegrationEvent(EventId);
