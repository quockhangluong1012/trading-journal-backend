using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Notifications.Common.Enums;
using TradingJournal.Modules.Notifications.Services;
using TradingJournal.Modules.Psychology.Events;

namespace TradingJournal.Modules.Notifications.EventHandlers;

/// <summary>
/// Handles StreakAlertEvent from the Psychology module.
/// Creates a notification and pushes it in real-time via SignalR.
/// </summary>
internal sealed class StreakAlertNotificationHandler(
    INotificationService notificationService,
    ILogger<StreakAlertNotificationHandler> logger) : INotificationHandler<StreakAlertEvent>
{
    public async Task Handle(StreakAlertEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "🎯 Handling streak alert for user {UserId}: {Type} x{Length}, PnL={PnL}, NewRecord={IsNewRecord}",
            notification.UserId, notification.StreakType, notification.StreakLength,
            notification.StreakPnl, notification.IsNewRecord);

        bool isLossStreak = notification.StreakType == "Loss";
        string emoji = isLossStreak ? "⚠️" : "🔥";

        string title = notification.IsNewRecord
            ? $"{emoji} New {notification.StreakType} Streak Record — {notification.StreakLength} trades"
            : $"{emoji} {notification.StreakType} Streak — {notification.StreakLength} consecutive";

        var priority = isLossStreak
            ? NotificationPriority.High
            : NotificationPriority.Normal;

        string metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            notification.StreakType,
            notification.StreakLength,
            notification.StreakPnl,
            notification.IsNewRecord
        });

        await notificationService.CreateAndPushAsync(
            notification.UserId,
            title,
            notification.Message,
            NotificationType.StreakAlert,
            priority,
            metadata,
            "/psychology",
            cancellationToken);
    }
}
