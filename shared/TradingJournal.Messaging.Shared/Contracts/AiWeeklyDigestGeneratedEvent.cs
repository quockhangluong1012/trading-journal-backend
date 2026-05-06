using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Contracts;

public sealed record AiWeeklyDigestGeneratedEvent(
    Guid EventId,
    int UserId,
    string Headline,
    string Summary,
    string FocusForNextWeek,
    IReadOnlyList<string> KeyWins,
    IReadOnlyList<string> KeyRisks,
    IReadOnlyList<string> ActionItems) : IntegrationEvent(EventId);