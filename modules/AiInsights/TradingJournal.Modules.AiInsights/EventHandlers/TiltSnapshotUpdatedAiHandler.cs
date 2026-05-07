using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.EventHandlers;

internal sealed class TiltSnapshotUpdatedAiHandler(
    IOpenRouterAIService aiService,
    IEventBus eventBus,
    ILogger<TiltSnapshotUpdatedAiHandler> logger) : INotificationHandler<TiltSnapshotUpdatedEvent>
{
    private const int MaxNotificationTitleLength = 200;
    private const int MaxNotificationMessageLength = 1000;
    private const int MaxActionItems = 10;
    private const int MaxActionItemLength = 200;

    public async Task Handle(TiltSnapshotUpdatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TiltScore < 35 && notification.ConsecutiveLosses == 0 && notification.RuleBreaksToday == 0)
        {
            return;
        }

        try
        {
            AiTiltInterventionResultDto? result = await aiService.AnalyzeTiltInterventionAsync(new AiTiltInterventionRequestDto(
                notification.UserId,
                notification.TiltScore,
                notification.TiltLevel,
                notification.ConsecutiveLosses,
                notification.TradesLastHour,
                notification.RuleBreaksToday,
                notification.TodayPnl,
                notification.CooldownUntil), cancellationToken);

            if (result is null || !result.ShouldNotify)
            {
                return;
            }

            string riskLevel = result.RiskLevel?.Trim() ?? string.Empty;
            if (!string.Equals(riskLevel, "high", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(riskLevel, "critical", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string tiltType = string.IsNullOrWhiteSpace(result.TiltType) ? "discipline" : result.TiltType.Trim();
            string title = Truncate(result.Title, MaxNotificationTitleLength);
            string message = Truncate(result.Message, MaxNotificationMessageLength);
            IReadOnlyList<string> actionItems = NormalizeActionItems(result.ActionItems);

            if (title.Length == 0 && message.Length == 0 && actionItems.Count == 0)
            {
                return;
            }

            logger.LogInformation(
                "AI tilt intervention detected for user {UserId} with risk level {RiskLevel}.",
                notification.UserId,
                riskLevel);

            await eventBus.PublishAsync(new AiTiltInterventionDetectedEvent(
                Guid.NewGuid(),
                notification.UserId,
                notification.TiltScore,
                notification.TiltLevel,
                riskLevel,
                tiltType,
                title,
                message,
                actionItems), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AI tilt intervention failed for user {UserId}.",
                notification.UserId);
        }
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
            .Take(MaxActionItems)];
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