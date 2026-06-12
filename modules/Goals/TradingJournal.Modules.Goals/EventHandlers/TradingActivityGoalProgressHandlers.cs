using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Goals.Services;

namespace TradingJournal.Modules.Goals.EventHandlers;

internal sealed class TradeJournaledGoalProgressHandler(IGoalActivityService goalActivityService)
    : INotificationHandler<TradeJournaledEvent>
{
    public Task Handle(TradeJournaledEvent notification, CancellationToken cancellationToken) =>
        goalActivityService.ApplyAsync(
            notification.EventId,
            notification.UserId,
            GoalMetricSource.TradeJournaledCount,
            1m,
            GoalActivitySourceType.TradeHistory,
            notification.TradeId,
            notification.JournaledAt,
            cancellationToken);
}

internal sealed class TradeClosedGoalProgressHandler(IGoalActivityService goalActivityService)
    : INotificationHandler<TradeClosedEvent>
{
    public async Task Handle(TradeClosedEvent notification, CancellationToken cancellationToken)
    {
        await goalActivityService.ApplyAsync(notification.EventId, notification.UserId,
            GoalMetricSource.TradeClosedCount, 1m, GoalActivitySourceType.TradeHistory,
            notification.TradeId, notification.ClosedDate, cancellationToken);

        if (notification.Pnl > 0m)
        {
            await goalActivityService.ApplyAsync(notification.EventId, notification.UserId,
                GoalMetricSource.WinningTradeCount, 1m, GoalActivitySourceType.TradeHistory,
                notification.TradeId, notification.ClosedDate, cancellationToken);
        }

        await goalActivityService.ApplyAsync(notification.EventId, notification.UserId,
            GoalMetricSource.TradingPnl, notification.Pnl, GoalActivitySourceType.TradeHistory,
            notification.TradeId, notification.ClosedDate, cancellationToken);
    }
}

internal sealed class BacktestSessionGoalProgressHandler(IGoalActivityService goalActivityService)
    : INotificationHandler<BacktestSessionCompletedEvent>
{
    public async Task Handle(BacktestSessionCompletedEvent notification, CancellationToken cancellationToken)
    {
        await goalActivityService.ApplyAsync(notification.EventId, notification.UserId,
            GoalMetricSource.BacktestSessionCount, 1m, GoalActivitySourceType.BacktestSession,
            notification.SessionId, notification.CompletedAt, cancellationToken);

        await goalActivityService.ApplyAsync(notification.EventId, notification.UserId,
            GoalMetricSource.BacktestTradeCount, notification.TradeCount, GoalActivitySourceType.BacktestSession,
            notification.SessionId, notification.CompletedAt, cancellationToken);

        await goalActivityService.ApplyAsync(notification.EventId, notification.UserId,
            GoalMetricSource.BacktestWinningTradeCount, notification.WinningTradeCount, GoalActivitySourceType.BacktestSession,
            notification.SessionId, notification.CompletedAt, cancellationToken);

        await goalActivityService.ApplyAsync(notification.EventId, notification.UserId,
            GoalMetricSource.BacktestPnl, notification.Pnl, GoalActivitySourceType.BacktestSession,
            notification.SessionId, notification.CompletedAt, cancellationToken);
    }
}
