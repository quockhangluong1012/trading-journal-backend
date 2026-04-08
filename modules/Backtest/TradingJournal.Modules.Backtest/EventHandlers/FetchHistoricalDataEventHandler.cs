using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Backtest.Hubs;
using TradingJournal.Modules.Backtest.Services;
using TradingJournal.Modules.Backtest.Events;

namespace TradingJournal.Modules.Backtest.EventHandlers;

/// <summary>
/// Handles the FetchHistoricalDataEvent by downloading M1 OHLCV data
/// from the configured provider, bulk-inserting into the database, and notifying
/// the frontend via SignalR when data is ready.
///
/// Only M1 candles are downloaded — higher timeframes are aggregated on-the-fly
/// by the CandleAggregationService.
/// </summary>
internal sealed class FetchHistoricalDataEventHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<FetchHistoricalDataEventHandler> logger)
    : INotificationHandler<FetchHistoricalDataEvent>
{
    public async Task Handle(FetchHistoricalDataEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting historical M1 data download for session {SessionId}: {Asset} from {Start} to {End}",
            notification.SessionId, notification.Asset, notification.StartDate, notification.EndDate);

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IMarketDataProvider marketDataProvider = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();
            IBacktestDbContext dbContext = scope.ServiceProvider.GetRequiredService<IBacktestDbContext>();
            IHubContext<BacktestHub> hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<BacktestHub>>();

            // Only download M1 candles — higher timeframes are computed from M1
            logger.LogInformation("Downloading M1 candles for {Asset}...", notification.Asset);

            List<OhlcvCandleData> candles = await marketDataProvider.DownloadOhlcvAsync(
                notification.Asset, Timeframe.M1, notification.StartDate, notification.EndDate, cancellationToken);

            int totalCandles = 0;

            if (candles.Count > 0)
            {
                // Use the asset symbol as-is (normalized when asset was registered)
                string symbol = notification.Asset;

                List<OhlcvCandle> entities = candles.Select(c => new OhlcvCandle
                {
                    Id = 0,
                    Asset = symbol,
                    Timeframe = Timeframe.M1,
                    Timestamp = c.Timestamp,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close,
                    Volume = c.Volume
                }).ToList();

                // Check for existing candles to avoid unique constraint violations
                HashSet<DateTime> existingTimestamps = (await dbContext.OhlcvCandles
                    .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                                && c.Timestamp >= notification.StartDate && c.Timestamp <= notification.EndDate)
                    .Select(c => c.Timestamp)
                    .ToListAsync(cancellationToken))
                    .ToHashSet();

                List<OhlcvCandle> newCandles = entities
                    .Where(e => !existingTimestamps.Contains(e.Timestamp))
                    .ToList();

                if (newCandles.Count > 0)
                {
                    // Bulk insert in chunks to avoid memory pressure
                    const int chunkSize = 5000;
                    for (int i = 0; i < newCandles.Count; i += chunkSize)
                    {
                        List<OhlcvCandle> chunk = newCandles.Skip(i).Take(chunkSize).ToList();
                        await dbContext.OhlcvCandles.AddRangeAsync(chunk, cancellationToken);
                        await dbContext.SaveChangesAsync(cancellationToken);

                        // Send progress update via SignalR
                        await hubContext.Clients
                            .Group($"backtest-{notification.SessionId}")
                            .SendAsync("DataProgress", new
                            {
                                notification.SessionId,
                                Timeframe = "M1",
                                CandleCount = Math.Min(i + chunkSize, newCandles.Count),
                                TotalExpected = newCandles.Count
                            }, cancellationToken);
                    }
                }

                totalCandles = newCandles.Count;

                logger.LogInformation(
                    "Saved {NewCount} new M1 candles ({Skipped} duplicates skipped)",
                    newCandles.Count, entities.Count - newCandles.Count);
            }
            else
            {
                logger.LogWarning("No M1 candles returned for {Asset}", notification.Asset);
            }

            // Mark session as data-ready
            BacktestSession? session = await dbContext.BacktestSessions
                .FirstOrDefaultAsync(s => s.Id == notification.SessionId, cancellationToken);

            if (session is not null)
            {
                session.IsDataReady = true;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Notify frontend that all data is ready
            await hubContext.Clients
                .Group($"backtest-{notification.SessionId}")
                .SendAsync("DataReady", new
                {
                    notification.SessionId,
                    TotalCandles = totalCandles
                }, cancellationToken);

            logger.LogInformation(
                "Historical M1 data download complete for session {SessionId}. Total candles: {Total}",
                notification.SessionId, totalCandles);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Failed to download historical data for session {SessionId}: {Asset}",
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
                        Error = "Failed to download market data. Please try again."
                    }, cancellationToken);
            }
            catch
            {
                // Best effort notification
            }
        }
    }
}
