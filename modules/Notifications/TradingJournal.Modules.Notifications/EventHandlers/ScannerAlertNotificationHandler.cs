using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Notifications.Common.Enums;
using TradingJournal.Modules.Notifications.Services;
using TradingJournal.Modules.Scanner.Events;

namespace TradingJournal.Modules.Notifications.EventHandlers;

/// <summary>
/// Handles ScannerAlertEvent from the Scanner module.
/// Creates a notification and pushes it in real-time via SignalR.
/// </summary>
internal sealed class ScannerAlertNotificationHandler(
    INotificationService notificationService,
    ILogger<ScannerAlertNotificationHandler> logger) : INotificationHandler<ScannerAlertEvent>
{
    public async Task Handle(ScannerAlertEvent notification, CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Handling scanner alert for user {UserId}: {Pattern} on {Symbol} ({Timeframe})",
            notification.UserId, notification.PatternType, notification.Symbol, notification.Timeframe);

        string title = $"🔍 {notification.PatternType} detected on {notification.Symbol}";
        string message = notification.Description;

        NotificationPriority priority = notification.ConfluenceScore switch
        {
            >= 3 => NotificationPriority.High,
            2 => NotificationPriority.Normal,
            _ => NotificationPriority.Low
        };

        string metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            notification.Symbol,
            notification.PatternType,
            notification.Timeframe,
            notification.Price,
            notification.ConfluenceScore
        });

        string actionUrl = $"/scanner?symbol={notification.Symbol}&pattern={notification.PatternType}";

        await notificationService.CreateAndPushAsync(
            notification.UserId,
            title,
            message,
            NotificationType.ScannerAlert,
            priority,
            metadata,
            actionUrl,
            cancellationToken);
    }
}
