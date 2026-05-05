using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Scanner.Hubs;

namespace TradingJournal.Modules.Scanner.Services.EconomicCalendar;

/// <summary>
/// Background service that continuously monitors the economic calendar and warns
/// all connected users to stop trading before high-impact economic news releases.
///
/// Behavior:
///   - Refreshes today's calendar every 2 hours
///   - Checks every 60 seconds for upcoming high-impact events
///   - Sends a "stop trading" warning 15 minutes before a high-impact event
///   - Sends a "news releasing now" alert when the event time arrives
///   - Tracks which events have been notified to avoid duplicate alerts
///
/// SignalR events pushed to ScannerHub:
///   - EconomicNewsWarning:  { Event details, MinutesUntilRelease }
///   - EconomicNewsReleased: { Event details }
///   - EconomicCalendarRefreshed: { TodayEventCount, HighImpactCount }
/// </summary>
internal sealed class EconomicCalendarBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<EconomicCalendarBackgroundService> logger) : BackgroundService
{
    /// <summary>
    /// How often to check for upcoming events (every 60 seconds).
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How far ahead to look for upcoming high-impact events (15 minutes).
    /// </summary>
    private static readonly TimeSpan WarningWindow = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How often to refresh the full calendar cache (every 2 hours).
    /// </summary>
    private static readonly TimeSpan CacheRefreshInterval = TimeSpan.FromHours(2);

    /// <summary>
    /// Tracks event IDs that have already been warned about to avoid duplicate notifications.
    /// Value = the warning stage (Warning = 1, Released = 2).
    /// </summary>
    private static readonly ConcurrentDictionary<string, int> NotifiedEvents = new();

    /// <summary>
    /// Last time we refreshed the calendar cache.
    /// </summary>
    private DateTime _lastCacheRefresh = DateTime.MinValue;

    /// <summary>
    /// Track the current date to clear notification history at midnight.
    /// </summary>
    private DateOnly _currentDate = DateOnly.FromDateTime(DateTime.UtcNow);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Economic calendar background service started.");

        // Wait for app startup
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCheckCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in economic calendar background service cycle.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        logger.LogInformation("Economic calendar background service stopped.");
    }

    private async Task RunCheckCycleAsync(CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();

        IEconomicCalendarProvider calendarProvider =
            scope.ServiceProvider.GetRequiredService<IEconomicCalendarProvider>();
        IHubContext<ScannerHub> hubContext =
            scope.ServiceProvider.GetRequiredService<IHubContext<ScannerHub>>();

        // Clear notification history at midnight UTC (new trading day)
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today != _currentDate)
        {
            _currentDate = today;
            NotifiedEvents.Clear();
            logger.LogInformation("Economic calendar: New day detected, cleared notification history.");
        }

        // Periodically refresh the cache
        if (DateTime.UtcNow - _lastCacheRefresh > CacheRefreshInterval)
        {
            await RefreshCalendarCacheAsync(calendarProvider, hubContext, ct);
            _lastCacheRefresh = DateTime.UtcNow;
        }

        // Check for upcoming high-impact events
        await CheckUpcomingEventsAsync(calendarProvider, hubContext, ct);
    }

    private async Task RefreshCalendarCacheAsync(
        IEconomicCalendarProvider provider,
        IHubContext<ScannerHub> hubContext,
        CancellationToken ct)
    {
        try
        {
            await provider.RefreshTodayCacheAsync(ct);

            List<EconomicEvent> todayEvents = await provider.GetTodayEventsAsync(ct);
            int highImpactCount = todayEvents.Count(e => e.Impact == EconomicImpact.High);

            // Broadcast refresh to all connected users
            await hubContext.Clients.All.SendAsync("EconomicCalendarRefreshed", new
            {
                TodayEventCount = todayEvents.Count,
                HighImpactCount = highImpactCount,
                Timestamp = DateTime.UtcNow
            }, ct);

            logger.LogInformation(
                "Economic calendar refreshed: {TotalEvents} events today, {HighImpact} high-impact",
                todayEvents.Count, highImpactCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh economic calendar cache.");
        }
    }

    private async Task CheckUpcomingEventsAsync(
        IEconomicCalendarProvider provider,
        IHubContext<ScannerHub> hubContext,
        CancellationToken ct)
    {
        List<EconomicEvent> upcomingEvents =
            await provider.GetUpcomingHighImpactEventsAsync(WarningWindow, ct);

        DateTime now = DateTime.UtcNow;

        foreach (EconomicEvent evt in upcomingEvents)
        {
            TimeSpan timeUntil = evt.EventDateUtc - now;
            int minutesUntil = (int)Math.Ceiling(timeUntil.TotalMinutes);

            // Stage 1: Warning (event is within 15 minutes and not yet warned)
            if (!NotifiedEvents.ContainsKey(evt.Id))
            {
                NotifiedEvents[evt.Id] = 1;

                await hubContext.Clients.All.SendAsync("EconomicNewsWarning", new
                {
                    EventId = evt.Id,
                    evt.EventName,
                    evt.Country,
                    evt.Currency,
                    Impact = evt.Impact.ToString(),
                    EventDateUtc = evt.EventDateUtc,
                    MinutesUntilRelease = minutesUntil,
                    evt.Forecast,
                    evt.Previous,
                    Message = $"⚠️ STOP TRADING — {evt.EventName} ({evt.Country}) releasing in {minutesUntil} minutes!",
                    Timestamp = DateTime.UtcNow
                }, ct);

                logger.LogWarning(
                    "Economic calendar WARNING: {EventName} ({Country}) in {Minutes} minutes. Broadcasting stop-trading alert.",
                    evt.EventName, evt.Country, minutesUntil);
            }
        }

        // Stage 2: Check for events that just released (within the last 2 minutes)
        List<EconomicEvent> todayEvents = await provider.GetTodayEventsAsync(ct);
        var justReleased = todayEvents
            .Where(e => e.Impact == EconomicImpact.High &&
                        e.EventDateUtc <= now &&
                        e.EventDateUtc >= now.AddMinutes(-2) &&
                        NotifiedEvents.TryGetValue(e.Id, out int stage) && stage == 1)
            .ToList();

        foreach (EconomicEvent evt in justReleased)
        {
            NotifiedEvents[evt.Id] = 2;

            await hubContext.Clients.All.SendAsync("EconomicNewsReleased", new
            {
                EventId = evt.Id,
                evt.EventName,
                evt.Country,
                evt.Currency,
                Impact = evt.Impact.ToString(),
                EventDateUtc = evt.EventDateUtc,
                evt.Actual,
                evt.Forecast,
                evt.Previous,
                Message = $"📊 {evt.EventName} ({evt.Country}) released! " +
                          (evt.Actual.HasValue ? $"Actual: {evt.Actual}" : "Awaiting data..."),
                Timestamp = DateTime.UtcNow
            }, ct);

            logger.LogInformation(
                "Economic calendar RELEASED: {EventName} ({Country}). Actual={Actual}, Forecast={Forecast}",
                evt.EventName, evt.Country, evt.Actual, evt.Forecast);
        }
    }
}
