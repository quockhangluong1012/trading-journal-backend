using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Scanner.Hubs;

namespace TradingJournal.Modules.Scanner.Services;

/// <summary>
/// Background service that continuously scans watchlists with their scanner enabled.
/// Each watchlist is scanned independently — users can turn on/off scanning per watchlist.
/// The service runs on the server and is NOT affected by client page refreshes.
/// </summary>
internal sealed class ScannerBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScannerBackgroundService> logger) : BackgroundService
{
    /// <summary>
    /// Minimum interval between full scan cycles (prevents tight-looping).
    /// </summary>
    private static readonly TimeSpan MinCycleInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scanner background service started.");

        // Small delay to allow app startup to complete
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunScanCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in scanner background service cycle.");
            }

            await Task.Delay(MinCycleInterval, stoppingToken);
        }

        logger.LogInformation("Scanner background service stopped.");
    }

    private async Task RunScanCycleAsync(CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();

        IScannerDbContext db = scope.ServiceProvider.GetRequiredService<IScannerDbContext>();
        IScannerEngine engine = scope.ServiceProvider.GetRequiredService<IScannerEngine>();
        IHubContext<ScannerHub> hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScannerHub>>();

        // Get all watchlists with scanner running (per-watchlist control)
        var activeWatchlists = await db.Watchlists
            .Where(w => w.IsScannerRunning && w.IsActive && !w.IsDisabled)
            .Select(w => new { w.Id, w.UserId, w.Name })
            .ToListAsync(ct);

        if (activeWatchlists.Count == 0) return;

        logger.LogDebug("Scanner cycle: {WatchlistCount} active watchlists to scan.", activeWatchlists.Count);

        foreach (var watchlist in activeWatchlists)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                DateTime cycleStart = DateTime.UtcNow;

                int alertsFound = await engine.ScanForWatchlistAsync(watchlist.Id, watchlist.UserId, ct);

                TimeSpan duration = DateTime.UtcNow - cycleStart;

                // Notify the user via SignalR that a watchlist scan cycle completed
                await hubContext.Clients.Group($"user-{watchlist.UserId}")
                    .SendAsync("WatchlistScanCompleted", new
                    {
                        WatchlistId = watchlist.Id,
                        WatchlistName = watchlist.Name,
                        AlertsFound = alertsFound,
                        Duration = duration.TotalMilliseconds,
                        Timestamp = DateTime.UtcNow
                    }, ct);

                logger.LogDebug(
                    "Scanner cycle for watchlist {WatchlistId} ({WatchlistName}), user {UserId}: {AlertsFound} alerts in {Duration}ms",
                    watchlist.Id, watchlist.Name, watchlist.UserId, alertsFound, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error scanning watchlist {WatchlistId} for user {UserId}",
                    watchlist.Id, watchlist.UserId);

                await hubContext.Clients.Group($"user-{watchlist.UserId}")
                    .SendAsync("WatchlistScannerError", new
                    {
                        WatchlistId = watchlist.Id,
                        WatchlistName = watchlist.Name,
                        Error = ex.Message
                    }, ct);
            }
        }
    }
}
