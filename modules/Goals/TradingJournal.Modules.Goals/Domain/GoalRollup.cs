namespace TradingJournal.Modules.Goals.Domain;

/// <summary>
/// Aggregates child completion into a parent "rollup" progress percentage.
/// A goal/milestone's own <see cref="TrackingProgress"/> ignores its children — a
/// manual goal sits at 0% until toggled even with every task done. Rollup fills
/// that gap: a parent with children reflects the average progress of those
/// children (e.g. 8/10 manual tasks done → 80%); a parent with no children falls
/// back to its own tracking progress.
/// </summary>
public static class GoalRollup
{
    public static decimal ItemProgress(ITrackableGoalItem item) => TrackingProgress.Calculate(
        item.TrackingMode,
        item.MetricDirection,
        item.StartValue,
        item.CurrentValue,
        item.TargetValue,
        item.IsCompleted);

    /// <summary>
    /// Milestone rollup from its directly-loaded tasks. Falls back to the
    /// milestone's own progress when it has no tasks.
    /// </summary>
    public static decimal ForMilestone(GoalMilestone milestone)
    {
        List<GoalTask> tasks = milestone.Tasks.ToList();
        return tasks.Count == 0
            ? ItemProgress(milestone)
            : decimal.Round(tasks.Average(ItemProgress), 2);
    }

    /// <summary>
    /// Goal rollup combining each milestone's rollup with the goal's loose tasks.
    /// Uses the goal's full task collection (grouped by milestone) so it works
    /// whether or not milestone.Tasks navigations were loaded. Falls back to the
    /// goal's own progress when it has neither milestones nor tasks.
    /// </summary>
    public static decimal ForGoal(Goal goal)
    {
        List<GoalTask> allTasks = goal.Tasks.ToList();
        var parts = new List<decimal>();

        foreach (GoalMilestone milestone in goal.Milestones)
        {
            List<GoalTask> milestoneTasks = allTasks.Where(task => task.MilestoneId == milestone.Id).ToList();
            parts.Add(milestoneTasks.Count == 0
                ? ItemProgress(milestone)
                : decimal.Round(milestoneTasks.Average(ItemProgress), 2));
        }

        parts.AddRange(allTasks.Where(task => task.MilestoneId == null).Select(ItemProgress));

        return parts.Count == 0
            ? ItemProgress(goal)
            : decimal.Round(parts.Average(), 2);
    }
}
