using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Notifications.Common.Enums;
using TradingJournal.Modules.Notifications.Services;
using TradingJournal.Modules.Psychology.Events;

namespace TradingJournal.Modules.Notifications.EventHandlers;

/// <summary>
/// Handles TiltCircuitBreakerEvent from the Psychology module.
/// Creates a high-priority notification and pushes it in real-time via SignalR.
/// </summary>
internal sealed class TiltCircuitBreakerNotificationHandler(
    INotificationService notificationService,
    ILogger<TiltCircuitBreakerNotificationHandler> logger) : INotificationHandler<TiltCircuitBreakerEvent>
{
    public async Task Handle(TiltCircuitBreakerEvent notification, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "🚨 Handling tilt circuit breaker for user {UserId}: Score={Score}, Level={Level}, Losses={Losses}",
            notification.UserId, notification.TiltScore, notification.TiltLevel, notification.ConsecutiveLosses);

        string title = $"🚨 Circuit Breaker — Tilt Score {notification.TiltScore}/100";

        string message = BuildMessage(notification);

        string metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            notification.TiltScore,
            notification.TiltLevel,
            notification.ConsecutiveLosses,
            notification.TradesLastHour,
            notification.RuleBreaksToday,
            notification.TodayPnl,
            notification.CooldownUntil
        });

        await notificationService.CreateAndPushAsync(
            notification.UserId,
            title,
            message,
            NotificationType.TiltWarning,
            NotificationPriority.Critical,
            metadata,
            "/psychology",
            cancellationToken);
    }

    private static string BuildMessage(TiltCircuitBreakerEvent e)
    {
        var parts = new List<string>
        {
            $"Your tilt level is {e.TiltLevel}."
        };

        if (e.ConsecutiveLosses > 0)
            parts.Add($"{e.ConsecutiveLosses} consecutive losses.");

        if (e.TradesLastHour > 3)
            parts.Add($"{e.TradesLastHour} trades in the last hour (overtrading).");

        if (e.RuleBreaksToday > 0)
            parts.Add($"{e.RuleBreaksToday} rule break(s) today.");

        if (e.TodayPnl < 0)
            parts.Add($"Today's PnL: {e.TodayPnl:C2}.");

        parts.Add($"Suggested cooldown until {e.CooldownUntil:HH:mm} UTC. Consider taking a break.");

        return string.Join(" ", parts);
    }
}
