using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Contracts;

public sealed record TiltSnapshotUpdatedEvent(
    Guid EventId,
    int UserId,
    int TiltScore,
    string TiltLevel,
    int ConsecutiveLosses,
    int TradesLastHour,
    int RuleBreaksToday,
    decimal TodayPnl,
    DateTime? CooldownUntil,
    DateTime RecordedAt) : IntegrationEvent(EventId);