using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Contracts;

public enum GoalItemKind
{
    Goal = 1,
    Milestone = 2,
    Task = 3,
}

public sealed record GoalItemCompletedEvent(
    Guid EventId,
    int UserId,
    int GoalId,
    int ItemId,
    GoalItemKind ItemKind,
    string Title,
    DateTime CompletedAt) : IntegrationEvent(EventId);
