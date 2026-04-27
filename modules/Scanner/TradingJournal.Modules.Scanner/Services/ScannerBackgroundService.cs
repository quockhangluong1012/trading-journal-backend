using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Scanner.Hubs;

namespace TradingJournal.Modules.Scanner.Services;

/// <summary>
/// Background service that continuously scans all active users' watchlists
/// for ICT patterns. Each user's scan runs at their configured interval.
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

        // Get all users with active scanner configs
        List<ScannerConfig> activeConfigs = await db.ScannerConfigs
            .Where(c => c.IsRunning && !c.IsDisabled)
            .ToListAsync(ct);

        if (activeConfigs.Count == 0) return;

        logger.LogDebug("Scanner cycle: {UserCount} active users to scan.", activeConfigs.Count);

        foreach (ScannerConfig config in activeConfigs)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                DateTime cycleStart = DateTime.UtcNow;

                int alertsFound = await engine.ScanForUserAsync(config.UserId, ct);

                TimeSpan duration = DateTime.UtcNow - cycleStart;

                // Notify the user via SignalR that a scan cycle completed
                await hubContext.Clients.Group($"user-{config.UserId}")
                    .SendAsync("ScanCycleCompleted", new
                    {
                        AlertsFound = alertsFound,
                        Duration = duration.TotalMilliseconds,
                        Timestamp = DateTime.UtcNow
                    }, ct);

                logger.LogDebug(
                    "Scanner cycle for user {UserId}: {AlertsFound} alerts in {Duration}ms",
                    config.UserId, alertsFound, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error scanning for user {UserId}", config.UserId);

                await hubContext.Clients.Group($"user-{config.UserId}")
                    .SendAsync("ScannerStatusChanged", new
                    {
                        Status = ScannerStatus.Error.ToString(),
                        Error = ex.Message
                    }, ct);
            }
        }
    }
}
