using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Events;

public record SummarizeTradingOrderEvent(Guid EventId,
    DateTime EventTime, int TradeHistoryId) : IntegrationEvent(EventId);
