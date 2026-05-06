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

            if (result.RiskLevel is not ("high" or "critical"))
            {
                return;
            }

            logger.LogInformation(
                "AI tilt intervention detected for user {UserId} with risk level {RiskLevel}.",
                notification.UserId,
                result.RiskLevel);

            await eventBus.PublishAsync(new AiTiltInterventionDetectedEvent(
                Guid.NewGuid(),
                notification.UserId,
                notification.TiltScore,
                notification.TiltLevel,
                result.RiskLevel,
                result.TiltType,
                result.Title,
                result.Message,
                result.ActionItems), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AI tilt intervention failed for user {UserId}.",
                notification.UserId);
        }
    }
}