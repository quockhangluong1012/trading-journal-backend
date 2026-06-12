using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Contracts;

namespace TradingJournal.Modules.Goals.Services;

internal interface IGoalActivityService
{
    Task ApplyAsync(
        Guid sourceEventId,
        int userId,
        GoalMetricSource metricSource,
        decimal delta,
        GoalActivitySourceType sourceType,
        int sourceId,
        DateTime recordedAt,
        CancellationToken cancellationToken);
}

internal sealed class GoalActivityService(
    IGoalDbContext context,
    IEventBus eventBus) : IGoalActivityService
{
    public async Task ApplyAsync(
        Guid sourceEventId,
        int userId,
        GoalMetricSource metricSource,
        decimal delta,
        GoalActivitySourceType sourceType,
        int sourceId,
        DateTime recordedAt,
        CancellationToken cancellationToken)
    {
        if (userId <= 0 || delta == 0m)
        {
            return;
        }

        HashSet<(GoalItemType ItemType, int ItemId)> appliedItems = (await context.ActivityLinks
                .AsNoTracking()
                .Where(link => link.SourceEventId == sourceEventId)
                .Select(link => new { link.ItemType, link.ItemId })
                .ToListAsync(cancellationToken))
            .Select(link => (link.ItemType, link.ItemId))
            .ToHashSet();

        List<Goal> goals = await context.Goals
            .Where(item => item.CreatedBy == userId
                && !item.IsCompleted
                && item.TrackingMode == TrackingMode.Metric
                && item.MetricSource == metricSource)
            .ToListAsync(cancellationToken);

        List<GoalMilestone> milestones = await context.Milestones
            .Where(item => item.CreatedBy == userId
                && !item.IsCompleted
                && item.TrackingMode == TrackingMode.Metric
                && item.MetricSource == metricSource)
            .ToListAsync(cancellationToken);

        List<GoalTask> tasks = await context.GoalTasks
            .Where(item => item.CreatedBy == userId
                && !item.IsCompleted
                && item.TrackingMode == TrackingMode.Metric
                && item.MetricSource == metricSource)
            .ToListAsync(cancellationToken);

        var completions = new List<GoalItemCompletedEvent>();
        bool changed = false;

        foreach (Goal goal in goals)
        {
            changed |= Apply(goal, goal.Id, GoalItemType.Goal, goal.Id, null, sourceEventId, userId,
                metricSource, delta, sourceType, sourceId, recordedAt, appliedItems, completions);
        }

        foreach (GoalMilestone milestone in milestones)
        {
            changed |= Apply(milestone, milestone.Id, GoalItemType.Milestone, milestone.GoalId, milestone.Id, sourceEventId, userId,
                metricSource, delta, sourceType, sourceId, recordedAt, appliedItems, completions);
        }

        foreach (GoalTask task in tasks)
        {
            changed |= Apply(task, task.Id, GoalItemType.Task, task.GoalId, task.MilestoneId, sourceEventId, userId,
                metricSource, delta, sourceType, sourceId, recordedAt, appliedItems, completions);
        }

        if (!changed)
        {
            return;
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (GoalItemCompletedEvent completion in completions)
        {
            await eventBus.PublishAsync(completion, cancellationToken);
        }
    }

    private bool Apply<T>(
        T item,
        int itemId,
        GoalItemType itemType,
        int goalId,
        int? milestoneId,
        Guid sourceEventId,
        int userId,
        GoalMetricSource metricSource,
        decimal delta,
        GoalActivitySourceType sourceType,
        int sourceId,
        DateTime recordedAt,
        ISet<(GoalItemType ItemType, int ItemId)> appliedItems,
        ICollection<GoalItemCompletedEvent> completions)
        where T : EntityBase<int>, ITrackableGoalItem
    {
        if (appliedItems.Contains((itemType, itemId)))
        {
            return false;
        }

        decimal? previousValue = item.CurrentValue;
        bool wasCompleted = item.IsCompleted;
        decimal currentValue = (item.CurrentValue ?? item.StartValue ?? 0m) + delta;

        item.CurrentValue = currentValue;
        item.IsCompleted = item.MetricDirection.HasValue
            && item.TargetValue.HasValue
            && TrackingProgress.IsMetricComplete(item.MetricDirection.Value, currentValue, item.TargetValue.Value);

        if (item.IsCompleted && !wasCompleted)
        {
            item.CompletedDate = recordedAt;
            completions.Add(new GoalItemCompletedEvent(
                Guid.NewGuid(),
                userId,
                goalId,
                itemId,
                ToItemKind(itemType),
                item.Title,
                recordedAt));
        }

        context.ProgressEntries.Add(new GoalProgressEntry
        {
            GoalId = goalId,
            ItemType = itemType,
            MilestoneId = milestoneId,
            GoalTaskId = itemType == GoalItemType.Task ? itemId : null,
            PreviousValue = previousValue,
            CurrentValue = currentValue,
            PreviousIsCompleted = wasCompleted,
            CurrentIsCompleted = item.IsCompleted,
            Note = $"Automatic progress from {sourceType} #{sourceId}.",
            CreatedBy = userId,
        });

        context.ActivityLinks.Add(new GoalActivityLink
        {
            GoalId = goalId,
            ItemType = itemType,
            ItemId = itemId,
            MetricSource = metricSource,
            SourceType = sourceType,
            SourceId = sourceId,
            SourceEventId = sourceEventId,
            Delta = delta,
            RecordedAt = recordedAt,
            CompletedItem = item.IsCompleted && !wasCompleted,
            CreatedBy = userId,
        });

        return true;
    }

    private static GoalItemKind ToItemKind(GoalItemType itemType) => itemType switch
    {
        GoalItemType.Goal => GoalItemKind.Goal,
        GoalItemType.Milestone => GoalItemKind.Milestone,
        GoalItemType.Task => GoalItemKind.Task,
        _ => throw new ArgumentOutOfRangeException(nameof(itemType), itemType, null),
    };
}
