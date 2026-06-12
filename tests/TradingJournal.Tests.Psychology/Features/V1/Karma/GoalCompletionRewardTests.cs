using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Psychology.Common.Enum;
using TradingJournal.Modules.Psychology.EventHandlers;

namespace TradingJournal.Tests.Psychology.Features.V1.Karma;

public sealed class GoalCompletionRewardTests
{
    [Theory]
    [InlineData(GoalItemKind.Task, KarmaActionType.GoalTaskCompleted, 10)]
    [InlineData(GoalItemKind.Milestone, KarmaActionType.GoalMilestoneCompleted, 25)]
    [InlineData(GoalItemKind.Goal, KarmaActionType.GoalCompleted, 50)]
    public void GetReward_ReturnsConfiguredKarma(
        GoalItemKind itemKind,
        KarmaActionType expectedAction,
        int expectedPoints)
    {
        GoalCompletionReward reward = GoalItemCompletedHandler.GetReward(itemKind);

        Assert.Equal(expectedAction, reward.ActionType);
        Assert.Equal(expectedPoints, reward.Points);
    }

    [Theory]
    [InlineData(AchievementType.FirstGoalTaskCompleted)]
    [InlineData(AchievementType.FirstGoalMilestoneCompleted)]
    [InlineData(AchievementType.FirstGoalCompleted)]
    [InlineData(AchievementType.GoalMaster)]
    public void GoalAchievementTypes_ArePersistable(AchievementType achievementType)
    {
        Assert.True((int)achievementType >= 300);
    }
}
