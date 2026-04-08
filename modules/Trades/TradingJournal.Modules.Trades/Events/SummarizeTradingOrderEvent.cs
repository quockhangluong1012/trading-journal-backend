using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Modules.Trades.Events;

public record SummarizeTradingOrderEvent(Guid EventId,
    DateTime EventTime, int TradeHistoryId) : IntegrationEvent(EventId);
