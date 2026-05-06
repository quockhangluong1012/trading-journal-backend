using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Notifications.Common.Enums;
using TradingJournal.Modules.Notifications.Services;

namespace TradingJournal.Modules.Notifications.EventHandlers;

internal sealed class AiTiltInterventionNotificationHandler(
    INotificationService notificationService,
    ILogger<AiTiltInterventionNotificationHandler> logger) : INotificationHandler<AiTiltInterventionDetectedEvent>
{
    public async Task Handle(AiTiltInterventionDetectedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Pushing AI tilt intervention for user {UserId} with risk level {RiskLevel}.",
            notification.UserId,
            notification.RiskLevel);

        string message = notification.ActionItems.Count > 0
            ? $"{notification.Message} Next: {notification.ActionItems[0]}"
            : notification.Message;

        string metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            notification.TiltScore,
            notification.TiltLevel,
            notification.RiskLevel,
            notification.TiltType,
            notification.ActionItems
        });

        await notificationService.CreateAndPushAsync(
            notification.UserId,
            Truncate(notification.Title, 200),
            Truncate(message, 1000),
            NotificationType.TiltWarning,
            MapPriority(notification.RiskLevel),
            Truncate(metadata, 4000),
            "/psychology",
            cancellationToken);
    }

    private static NotificationPriority MapPriority(string riskLevel)
    {
        return riskLevel.ToLowerInvariant() switch
        {
            "critical" => NotificationPriority.Critical,
            "high" => NotificationPriority.High,
            "medium" => NotificationPriority.Normal,
            _ => NotificationPriority.Low,
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}