using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Psychology.Services;

namespace TradingJournal.Modules.Psychology.EventHandlers;

internal sealed class TradeClosedTiltRefreshHandler(
    ITiltDetectionService tiltDetectionService,
    ILogger<TradeClosedTiltRefreshHandler> logger) : INotificationHandler<TradeClosedEvent>
{
    public async Task Handle(TradeClosedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Refreshing tilt after trade close for user {UserId} and trade {TradeId}.",
            notification.UserId,
            notification.TradeId);

        await tiltDetectionService.RecalculateTiltAsync(notification.UserId, cancellationToken);
    }
}