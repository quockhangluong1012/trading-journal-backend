using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Contracts;

public sealed record TradeClosedEvent(
    Guid EventId,
    int UserId,
    int TradeId,
    DateTime ClosedDate,
    decimal Pnl) : IntegrationEvent(EventId);