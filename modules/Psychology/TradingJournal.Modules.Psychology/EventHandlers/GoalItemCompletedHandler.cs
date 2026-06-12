using TradingJournal.Messaging.Shared.Contracts;

namespace TradingJournal.Modules.Psychology.EventHandlers;

internal sealed record GoalCompletionReward(KarmaActionType ActionType, int Points, string Label);

internal sealed class GoalItemCompletedHandler(IKarmaService karmaService)
    : INotificationHandler<GoalItemCompletedEvent>
{
    public async Task Handle(GoalItemCompletedEvent notification, CancellationToken cancellationToken)
    {
        GoalCompletionReward reward = GetReward(notification.ItemKind);

        await karmaService.AwardKarmaAsync(
            notification.UserId,
            reward.ActionType,
            $"{reward.Label} completed: {notification.Title}",
            notification.ItemId,
            reward.Points,
            cancellationToken);
    }

    internal static GoalCompletionReward GetReward(GoalItemKind itemKind) => itemKind switch
    {
        GoalItemKind.Task => new GoalCompletionReward(KarmaActionType.GoalTaskCompleted, 10, "Goal task"),
        GoalItemKind.Milestone => new GoalCompletionReward(KarmaActionType.GoalMilestoneCompleted, 25, "Goal milestone"),
        GoalItemKind.Goal => new GoalCompletionReward(KarmaActionType.GoalCompleted, 50, "Goal"),
        _ => throw new ArgumentOutOfRangeException(nameof(itemKind), itemKind, null),
    };
}
