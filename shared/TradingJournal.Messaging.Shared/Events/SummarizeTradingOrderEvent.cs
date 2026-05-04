using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Events;

public record SummarizeTradingOrderEvent(Guid EventId,
    DateTimeOffset EventTime, int TradeHistoryId) : IntegrationEvent(EventId);
