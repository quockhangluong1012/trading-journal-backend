using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Backtest.Hubs;
using TradingJournal.Modules.Backtest.Events;

namespace TradingJournal.Modules.Backtest.EventHandlers;

/// <summary>
/// Handles the FetchHistoricalDataEvent by verifying that pre-synced M1 candle data
/// already exists in the database for the requested asset and date range.
///
/// Data is pre-downloaded by the DataSyncBackgroundService when an asset is registered.
/// This handler simply validates availability, marks the session as data-ready,
/// and notifies the frontend via SignalR — making session creation nearly instant.
/// </summary>
internal sealed class FetchHistoricalDataEventHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<FetchHistoricalDataEventHandler> logger)
    : INotificationHandler<FetchHistoricalDataEvent>
{
    public async Task Handle(FetchHistoricalDataEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IBacktestDbContext dbContext = scope.ServiceProvider.GetRequiredService<IBacktestDbContext>();
            IHubContext<BacktestHub> hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<BacktestHub>>();

            // Query the count of pre-synced M1 candles for this asset and date range
            string symbol = notification.Asset.Trim().ToUpperInvariant();

            int totalCandles = await dbContext.OhlcvCandles
                .CountAsync(c => c.Asset == symbol
                                 && c.Timeframe == Timeframe.M1
                                 && c.Timestamp >= notification.StartDate
                                 && c.Timestamp <= notification.EndDate,
                    cancellationToken);

            if (totalCandles == 0)
            {
                logger.LogWarning(
                    "No pre-synced M1 candles found for {Asset} between {Start} and {End}. " +
                    "Ensure the asset has been registered and synced via Admin before creating a session.",
                    symbol, notification.StartDate, notification.EndDate);

                await hubContext.Clients
                    .Group($"backtest-{notification.SessionId}")
                    .SendAsync("DataError", new
                    {
                        notification.SessionId,
                        Error = $"No candle data available for {symbol}. Please ensure the asset has been synced in the Admin portal."
                    }, cancellationToken);

                return;
            }

            // Mark session as data-ready
            BacktestSession? session = await dbContext.BacktestSessions
                .FirstOrDefaultAsync(s => s.Id == notification.SessionId, cancellationToken);

            if (session is not null)
            {
                session.IsDataReady = true;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Notify frontend that data is ready
            await hubContext.Clients
                .Group($"backtest-{notification.SessionId}")
                .SendAsync("DataReady", new
                {
                    notification.SessionId,
                    TotalCandles = totalCandles
                }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Failed to verify historical data for session {SessionId}: {Asset}",
                notification.SessionId, notification.Asset);

            // Attempt to notify frontend of failure
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                IHubContext<BacktestHub> hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<BacktestHub>>();

                await hubContext.Clients
                    .Group($"backtest-{notification.SessionId}")
                    .SendAsync("DataError", new
                    {
                        notification.SessionId,
                        Error = "Failed to verify market data availability. Please try again."
                    }, cancellationToken);
            }
            catch
            {
                // Best effort notification
            }
        }
    }
}

