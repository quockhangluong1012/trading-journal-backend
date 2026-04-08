using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Backtest.Services;

namespace TradingJournal.Modules.Backtest.Services;

/// <summary>
/// Background service that continuously syncs M1 candle data for registered assets.
///
/// Flow:
///   1. Polls every 30s for assets with status Syncing
///   2. For each, downloads the next month of M1 data from the configured provider
///   3. Bulk inserts into DB, updates progress
///   4. When all months are synced, marks asset as Ready
///   5. Handles errors gracefully — retries on next cycle
///
/// The service also runs a daily incremental sync for Ready assets
/// to keep data up to date.
/// </summary>
internal sealed class DataSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<DataSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DataSyncBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAndSyncingAssets(stoppingToken);
                await ProcessIncrementalSync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DataSyncBackgroundService loop.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        logger.LogInformation("DataSyncBackgroundService stopped.");
    }

    /// <summary>
    /// Process assets that are Pending or Syncing — download the next month of data.
    /// </summary>
    private async Task ProcessPendingAndSyncingAssets(CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IBacktestDbContext db = scope.ServiceProvider.GetRequiredService<IBacktestDbContext>();
        IMarketDataProvider provider = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();

        List<BacktestAsset> assets = await db.BacktestAssets
            .Where(a => a.SyncStatus == AssetSyncStatus.Pending || a.SyncStatus == AssetSyncStatus.Syncing)
            .Where(a => a.DataProvider != "CSV") // CSV assets are imported, not synced
            .ToListAsync(ct);

        foreach (BacktestAsset asset in assets)
        {
            try
            {
                // Mark as syncing
                if (asset.SyncStatus == AssetSyncStatus.Pending)
                {
                    asset.SyncStatus = AssetSyncStatus.Syncing;
                    await db.SaveChangesAsync(ct);
                }

                await SyncNextMonth(db, provider, asset, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to sync asset {Symbol}: {Error}", asset.Symbol, ex.Message);
                asset.SyncStatus = AssetSyncStatus.Error;
                asset.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>
    /// Downloads the next unsynced month of M1 data for the given asset.
    /// </summary>
    private async Task SyncNextMonth(
        IBacktestDbContext db,
        IMarketDataProvider provider,
        BacktestAsset asset,
        CancellationToken ct)
    {
        // Determine the next month to sync
        DateTime monthStart = asset.LastSyncedDate.HasValue
            ? new DateTime(asset.LastSyncedDate.Value.Year, asset.LastSyncedDate.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(1)
            : new DateTime(asset.DataStartDate.Year, asset.DataStartDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        DateTime endDate = asset.DataEndDate ?? DateTime.UtcNow;

        if (monthStart >= endDate)
        {
            // All months synced — mark as Ready
            asset.SyncStatus = AssetSyncStatus.Ready;
            asset.LastError = null;
            asset.TotalCandles = await db.OhlcvCandles
                .Where(c => c.Asset == asset.Symbol && c.Timeframe == Timeframe.M1)
                .LongCountAsync(ct);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Asset {Symbol} sync COMPLETE. Total M1 candles: {Total}",
                asset.Symbol, asset.TotalCandles);
            return;
        }

        DateTime monthEnd = monthStart.AddMonths(1).AddSeconds(-1);
        if (monthEnd > endDate) monthEnd = endDate;

        logger.LogInformation(
            "Syncing {Symbol} — month {Month:yyyy-MM} ({Start} to {End})",
            asset.Symbol, monthStart, monthStart, monthEnd);

        // Download from provider (always M1)
        List<OhlcvCandleData> candles = await provider.DownloadOhlcvAsync(
            asset.Symbol, Timeframe.M1, monthStart, monthEnd, ct);

        if (candles.Count > 0)
        {
            // Check for existing candles to avoid duplicates
            HashSet<DateTime> existing = (await db.OhlcvCandles
                .Where(c => c.Asset == asset.Symbol && c.Timeframe == Timeframe.M1
                            && c.Timestamp >= monthStart && c.Timestamp <= monthEnd)
                .Select(c => c.Timestamp)
                .ToListAsync(ct))
                .ToHashSet();

            List<OhlcvCandle> newCandles = candles
                .Where(c => !existing.Contains(c.Timestamp))
                .Select(c => new OhlcvCandle
                {
                    Id = 0,
                    Asset = asset.Symbol,
                    Timeframe = Timeframe.M1,
                    Timestamp = c.Timestamp,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close,
                    Volume = c.Volume
                })
                .ToList();

            if (newCandles.Count > 0)
            {
                // Bulk insert in chunks to avoid memory pressure
                const int chunkSize = 5000;
                for (int i = 0; i < newCandles.Count; i += chunkSize)
                {
                    List<OhlcvCandle> chunk = newCandles.Skip(i).Take(chunkSize).ToList();
                    await db.OhlcvCandles.AddRangeAsync(chunk, ct);
                    await db.SaveChangesAsync(ct);
                }

                logger.LogInformation(
                    "Saved {Count} new M1 candles for {Symbol} ({Month:yyyy-MM})",
                    newCandles.Count, asset.Symbol, monthStart);
            }
        }

        // Update progress
        asset.LastSyncedDate = monthEnd;
        asset.TotalCandles = await db.OhlcvCandles
            .Where(c => c.Asset == asset.Symbol && c.Timeframe == Timeframe.M1)
            .LongCountAsync(ct);
        asset.LastError = null;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Once daily, check Ready assets and sync any new candles since last sync.
    /// </summary>
    private async Task ProcessIncrementalSync(CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IBacktestDbContext db = scope.ServiceProvider.GetRequiredService<IBacktestDbContext>();

        List<BacktestAsset> readyAssets = await db.BacktestAssets
            .Where(a => a.SyncStatus == AssetSyncStatus.Ready
                        && a.DataProvider != "CSV"
                        && a.LastSyncedDate.HasValue
                        && a.LastSyncedDate.Value < DateTime.UtcNow.Date.AddDays(-1))
            .ToListAsync(ct);

        if (readyAssets.Count == 0) return;

        IMarketDataProvider provider = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();

        foreach (BacktestAsset asset in readyAssets)
        {
            try
            {
                // Temporarily set to syncing for incremental
                await SyncNextMonth(db, provider, asset, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Incremental sync failed for {Symbol}", asset.Symbol);
            }
        }
    }
}
