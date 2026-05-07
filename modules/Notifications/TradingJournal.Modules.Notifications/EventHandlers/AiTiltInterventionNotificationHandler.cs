using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Notifications.Common.Enums;
using TradingJournal.Modules.Notifications.Services;

namespace TradingJournal.Modules.Notifications.EventHandlers;

internal sealed class AiTiltInterventionNotificationHandler(
    INotificationService notificationService,
    ILogger<AiTiltInterventionNotificationHandler> logger) : INotificationHandler<AiTiltInterventionDetectedEvent>
{
    private const int MaxTitleLength = 200;
    private const int MaxMessageLength = 1000;
    private const int MaxMetadataLength = 4000;
    private const int MaxActionItemsInMetadata = 10;
    private const int MaxActionItemLength = 200;
    private const string NextStepPrefix = "Next: ";
    private const string NextStepSeparator = " Next: ";

    public async Task Handle(AiTiltInterventionDetectedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Pushing AI tilt intervention for user {UserId} with risk level {RiskLevel}.",
            notification.UserId,
            notification.RiskLevel);

        IReadOnlyList<string> actionItems = NormalizeActionItems(notification.ActionItems);
        string message = BuildMessage(notification.Message, actionItems);

        string metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            notification.TiltScore,
            notification.TiltLevel,
            notification.RiskLevel,
            notification.TiltType,
            ActionItems = actionItems
        });

        await notificationService.CreateAndPushAsync(
            notification.UserId,
            Truncate(notification.Title, MaxTitleLength),
            message,
            NotificationType.TiltWarning,
            MapPriority(notification.RiskLevel),
            Truncate(metadata, MaxMetadataLength),
            "/psychology",
            cancellationToken);
    }

    private static NotificationPriority MapPriority(string? riskLevel)
    {
        return riskLevel?.Trim().ToLowerInvariant() switch
        {
            "critical" => NotificationPriority.Critical,
            "high" => NotificationPriority.High,
            "medium" => NotificationPriority.Normal,
            _ => NotificationPriority.Low,
        };
    }

    private static string BuildMessage(string? baseMessage, IReadOnlyList<string> actionItems)
    {
        string normalizedBaseMessage = Truncate(baseMessage, MaxMessageLength);
        if (actionItems.Count == 0)
        {
            return normalizedBaseMessage;
        }

        string nextStep = $"{NextStepPrefix}{actionItems[0]}";
        if (normalizedBaseMessage.Length == 0)
        {
            return Truncate(nextStep, MaxMessageLength);
        }

        int availableBaseLength = MaxMessageLength - NextStepSeparator.Length - actionItems[0].Length;
        if (availableBaseLength <= 0)
        {
            return Truncate(nextStep, MaxMessageLength);
        }

        return $"{Truncate(normalizedBaseMessage, availableBaseLength)}{NextStepSeparator}{actionItems[0]}";
    }

    private static IReadOnlyList<string> NormalizeActionItems(IReadOnlyList<string>? actionItems)
    {
        if (actionItems is null || actionItems.Count == 0)
        {
            return [];
        }

        return [.. actionItems
            .Where(actionItem => !string.IsNullOrWhiteSpace(actionItem))
            .Select(actionItem => Truncate(actionItem, MaxActionItemLength))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxActionItemsInMetadata)];
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}