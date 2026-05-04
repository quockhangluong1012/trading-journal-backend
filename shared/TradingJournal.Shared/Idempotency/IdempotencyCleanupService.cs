using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Shared.Idempotency;

/// <summary>
/// Background service that periodically cleans up expired idempotency records.
/// Runs every 6 hours by default.
/// </summary>
internal sealed class IdempotencyCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<IdempotencyCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Idempotency cleanup service started (interval: {Interval})", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);

                using IServiceScope scope = scopeFactory.CreateScope();
                IIdempotencyStore store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

                await store.CleanupExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down gracefully
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Idempotency cleanup failed");
            }
        }
    }
}
