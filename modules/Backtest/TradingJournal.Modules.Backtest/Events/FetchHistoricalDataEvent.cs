using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Modules.Backtest.Events;

/// <summary>
/// Integration event published when a new backtest session is created.
/// Triggers the background worker to download historical market data from Twelve Data.
/// </summary>
public sealed record FetchHistoricalDataEvent(
    Guid EventId,
    int SessionId,
    string Asset,
    DateTime StartDate,
    DateTime EndDate,
    int UserId) : IntegrationEvent(EventId);
