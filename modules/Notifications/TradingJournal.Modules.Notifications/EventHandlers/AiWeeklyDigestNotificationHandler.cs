using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Notifications.Common.Enums;
using TradingJournal.Modules.Notifications.Services;

namespace TradingJournal.Modules.Notifications.EventHandlers;

internal sealed class AiWeeklyDigestNotificationHandler(
    INotificationService notificationService,
    ILogger<AiWeeklyDigestNotificationHandler> logger) : INotificationHandler<AiWeeklyDigestGeneratedEvent>
{
    public async Task Handle(AiWeeklyDigestGeneratedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Pushing AI weekly digest notification for user {UserId}.",
            notification.UserId);

        string message = string.IsNullOrWhiteSpace(notification.FocusForNextWeek)
            ? notification.Summary
            : $"{notification.Summary} Focus: {notification.FocusForNextWeek}";

        string metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            notification.KeyWins,
            notification.KeyRisks,
            notification.ActionItems,
            notification.FocusForNextWeek,
        });

        await notificationService.CreateAndPushAsync(
            notification.UserId,
            Truncate(string.IsNullOrWhiteSpace(notification.Headline) ? "Weekly AI Digest" : notification.Headline, 200),
            Truncate(message, 1000),
            NotificationType.AiInsight,
            NotificationPriority.Normal,
            Truncate(metadata, 4000),
            "/review",
            cancellationToken);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}