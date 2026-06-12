using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Contracts;

public sealed record TradeJournaledEvent(
    Guid EventId,
    int UserId,
    int TradeId,
    DateTime JournaledAt) : IntegrationEvent(EventId);
