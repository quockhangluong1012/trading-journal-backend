namespace TradingJournal.Tests.Goals;

public sealed class GoalRollupTests
{
    private static GoalTask ManualTask(int id, int? milestoneId, bool completed) => new()
    {
        Id = id,
        MilestoneId = milestoneId,
        TrackingMode = TrackingMode.Manual,
        IsCompleted = completed,
    };

    [Fact]
    public void ForGoal_WithoutChildren_FallsBackToOwnProgress()
    {
        var goal = new Goal { TrackingMode = TrackingMode.Manual, IsCompleted = false };
        Assert.Equal(0m, GoalRollup.ForGoal(goal));

        goal.IsCompleted = true;
        Assert.Equal(100m, GoalRollup.ForGoal(goal));
    }

    [Fact]
    public void ForGoal_AveragesLooseTaskCompletion()
    {
        var goal = new Goal
        {
            TrackingMode = TrackingMode.Manual,
            IsCompleted = false,
            Tasks =
            [
                ManualTask(1, null, true),
                ManualTask(2, null, true),
                ManualTask(3, null, false),
                ManualTask(4, null, false),
            ],
        };

        // 2 of 4 loose tasks complete → 50%, independent of the goal's own toggle.
        Assert.Equal(50m, GoalRollup.ForGoal(goal));
    }

    [Fact]
    public void ForGoal_CombinesMilestoneRollupWithLooseTasks()
    {
        var milestone = new GoalMilestone { Id = 10, TrackingMode = TrackingMode.Manual };
        var goal = new Goal
        {
            TrackingMode = TrackingMode.Manual,
            Milestones = [milestone],
            Tasks =
            [
                ManualTask(1, 10, true),   // milestone task — done
                ManualTask(2, 10, false),  // milestone task — not done  → milestone rollup 50%
                ManualTask(3, null, true), // loose task — done           → 100%
            ],
        };

        // (milestone 50% + loose 100%) / 2 = 75%
        Assert.Equal(75m, GoalRollup.ForGoal(goal));
    }

    [Fact]
    public void ForMilestone_AveragesItsTasks()
    {
        var milestone = new GoalMilestone
        {
            TrackingMode = TrackingMode.Manual,
            Tasks = [ManualTask(1, 5, true), ManualTask(2, 5, false)],
        };

        Assert.Equal(50m, GoalRollup.ForMilestone(milestone));
    }

    [Fact]
    public void ForMilestone_WithoutTasks_UsesOwnMetricProgress()
    {
        var milestone = new GoalMilestone
        {
            TrackingMode = TrackingMode.Metric,
            MetricDirection = MetricDirection.AtLeast,
            StartValue = 0m,
            CurrentValue = 30m,
            TargetValue = 100m,
        };

        Assert.Equal(30m, GoalRollup.ForMilestone(milestone));
    }
}
