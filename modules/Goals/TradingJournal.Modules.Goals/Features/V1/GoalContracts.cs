namespace TradingJournal.Modules.Goals.Features.V1;

public sealed record TrackingInput(
    TrackingMode Mode,
    string? MetricName = null,
    string? MetricUnit = null,
    MetricDirection? Direction = null,
    decimal? StartValue = null,
    decimal? TargetValue = null)
{
    public static TrackingInput Manual { get; } = new(TrackingMode.Manual);
}

public sealed record ProgressResult(
    decimal? CurrentValue,
    decimal ProgressPercent,
    bool IsCompleted,
    DateTime? CompletedDate);

public sealed record TrackingSnapshot(
    TrackingMode Mode,
    string? MetricName,
    string? MetricUnit,
    MetricDirection? Direction,
    decimal? StartValue,
    decimal? CurrentValue,
    decimal? TargetValue,
    decimal ProgressPercent,
    bool IsCompleted,
    DateTime? CompletedDate);

public sealed record GoalTaskView(
    int Id,
    int? MilestoneId,
    string Title,
    string? Description,
    DateTime? DueDate,
    int SortOrder,
    TrackingSnapshot Tracking);

public sealed record GoalMilestoneView(
    int Id,
    string Title,
    string? Description,
    DateTime? DueDate,
    int SortOrder,
    TrackingSnapshot Tracking,
    IReadOnlyList<GoalTaskView> Tasks);

public sealed record ProgressEntryView(
    int Id,
    GoalItemType ItemType,
    int? MilestoneId,
    int? TaskId,
    decimal? PreviousValue,
    decimal? CurrentValue,
    bool PreviousIsCompleted,
    bool CurrentIsCompleted,
    string? Note,
    DateTime RecordedAt);

public sealed record GoalSummary(
    int Id,
    string Title,
    string? Description,
    DateTime? StartDate,
    DateTime? DueDate,
    TrackingSnapshot Tracking,
    int MilestoneCount,
    int TaskCount,
    int CompletedTaskCount,
    DateTime CreatedDate,
    DateTime? UpdatedDate);

public sealed record GoalDetail(
    int Id,
    string Title,
    string? Description,
    DateTime? StartDate,
    DateTime? DueDate,
    TrackingSnapshot Tracking,
    IReadOnlyList<GoalMilestoneView> Milestones,
    IReadOnlyList<GoalTaskView> Tasks,
    IReadOnlyList<ProgressEntryView> ProgressHistory,
    DateTime CreatedDate,
    DateTime? UpdatedDate);

internal sealed class TrackingInputValidator : AbstractValidator<TrackingInput>
{
    public TrackingInputValidator()
    {
        RuleFor(x => x.Mode).IsInEnum();

        When(x => x.Mode == TrackingMode.Metric, () =>
        {
            RuleFor(x => x.MetricName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.MetricUnit).MaximumLength(50);
            RuleFor(x => x.Direction).NotNull().IsInEnum();
            RuleFor(x => x.StartValue).NotNull();
            RuleFor(x => x.TargetValue).NotNull();
            RuleFor(x => x.TargetValue).Must((input, target) =>
            {
                if (input.Direction is null || input.StartValue is null || target is null)
                {
                    return true;
                }

                return input.Direction == MetricDirection.AtLeast
                    ? target > input.StartValue
                    : target < input.StartValue;
            }).WithMessage("Target value must move in the configured metric direction.");
        });
    }
}

internal static class GoalTrackingMapper
{
    public static void Apply(ITrackableGoalItem item, TrackingInput tracking)
    {
        item.TrackingMode = tracking.Mode;
        item.MetricName = tracking.Mode == TrackingMode.Metric ? tracking.MetricName?.Trim() : null;
        item.MetricUnit = tracking.Mode == TrackingMode.Metric ? Normalize(tracking.MetricUnit) : null;
        item.MetricDirection = tracking.Mode == TrackingMode.Metric ? tracking.Direction : null;
        item.StartValue = tracking.Mode == TrackingMode.Metric ? tracking.StartValue : null;
        item.CurrentValue = tracking.Mode == TrackingMode.Metric ? tracking.StartValue : null;
        item.TargetValue = tracking.Mode == TrackingMode.Metric ? tracking.TargetValue : null;
        item.IsCompleted = false;
        item.CompletedDate = null;
    }

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static TrackingSnapshot ToSnapshot(ITrackableGoalItem item) => new(
        item.TrackingMode,
        item.MetricName,
        item.MetricUnit,
        item.MetricDirection,
        item.StartValue,
        item.CurrentValue,
        item.TargetValue,
        TrackingProgress.Calculate(
            item.TrackingMode,
            item.MetricDirection,
            item.StartValue,
            item.CurrentValue,
            item.TargetValue,
            item.IsCompleted),
        item.IsCompleted,
        item.CompletedDate);

    public static GoalTaskView ToView(GoalTask task) => new(
        task.Id,
        task.MilestoneId,
        task.Title,
        task.Description,
        task.DueDate,
        task.SortOrder,
        ToSnapshot(task));
}
