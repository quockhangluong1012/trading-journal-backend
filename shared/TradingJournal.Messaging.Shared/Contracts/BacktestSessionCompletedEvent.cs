using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Contracts;

public sealed record BacktestSessionCompletedEvent(
    Guid EventId,
    int UserId,
    int SessionId,
    DateTime CompletedAt,
    int TradeCount,
    int WinningTradeCount,
    decimal Pnl) : IntegrationEvent(EventId);
